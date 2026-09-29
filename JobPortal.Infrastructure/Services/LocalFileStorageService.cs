using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.Common.Models;
using JobPortal.Domain.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPortal.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _hostRoot;
    private readonly string _baseStoragePath;
    private readonly FileStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IHostEnvironment hostEnvironment,
        IOptions<FileStorageOptions> options,
        ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _hostRoot = Path.GetFullPath(hostEnvironment.ContentRootPath);
        _baseStoragePath = Path.GetFullPath(Path.Combine(_hostRoot, _options.ResumeStoragePath));

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<FileStorageResult> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string subDirectory,
        CancellationToken cancellationToken = default)
    {
        var rawExtension = Path.GetExtension(originalFileName);
        var extension = string.IsNullOrWhiteSpace(rawExtension) ? string.Empty : rawExtension.ToLowerInvariant();

        // 1. Generate unique, non-guessable storage filename
        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        // 2. Sanitize and construct target directory
        var sanitizedSubDir = subDirectory.Replace("..", "").Trim('/', '\\');
        var targetDir = Path.GetFullPath(Path.Combine(_baseStoragePath, sanitizedSubDir));

        if (!targetDir.StartsWith(_baseStoragePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Potential path traversal attempt detected for directory: {Directory}", subDirectory);
            throw new ValidationException("StoragePath", "Invalid target directory path.");
        }

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var fullFilePath = Path.Combine(targetDir, storedFileName);
        var relativeStoragePath = Path.Combine(_options.ResumeStoragePath, sanitizedSubDir, storedFileName).Replace('\\', '/');

        // 3. Stream file contents to disk
        await using (var destinationStream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        var fileInfo = new FileInfo(fullFilePath);

        _logger.LogInformation("File saved securely to storage: {RelativePath} ({SizeBytes} bytes)", relativeStoragePath, fileInfo.Length);

        return new FileStorageResult
        {
            StoragePath = relativeStoragePath,
            StoredFileName = storedFileName,
            FileSizeBytes = fileInfo.Length,
            ContentType = contentType
        };
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullFilePath = ResolveAndValidateFullPath(storagePath);

        if (!File.Exists(fullFilePath))
        {
            _logger.LogWarning("File not found on storage: {StoragePath}", storagePath);
            throw new EntityNotFoundException("Physical file was not found on storage.");
        }

        var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult<Stream>(stream);
    }

    public Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullFilePath = ResolveAndValidateFullPath(storagePath);

            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
                _logger.LogInformation("File deleted from storage: {StoragePath}", storagePath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from storage: {StoragePath}", storagePath);
            return Task.FromResult(false);
        }
    }

    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullFilePath = ResolveAndValidateFullPath(storagePath);
            return Task.FromResult(File.Exists(fullFilePath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private string ResolveAndValidateFullPath(string relativePath)
    {
        var sanitized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(_hostRoot, sanitized));

        if (!fullPath.StartsWith(_baseStoragePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt in storage access: {Path}", relativePath);
            throw new ValidationException("StoragePath", "Invalid storage path.");
        }

        return fullPath;
    }
}
