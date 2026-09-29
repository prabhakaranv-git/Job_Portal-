using JobPortal.Application.Common.Models;

namespace JobPortal.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileStorageResult> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string subDirectory,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
