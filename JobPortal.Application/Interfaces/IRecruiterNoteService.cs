using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.RecruiterNote;

namespace JobPortal.Application.Interfaces;

public interface IRecruiterNoteService
{
    Task<ApiResponse<RecruiterNoteResponse>> CreateNoteAsync(
        Guid applicationId,
        CreateRecruiterNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedResponse<RecruiterNoteResponse>>> GetNotesAsync(
        Guid applicationId,
        RecruiterNoteQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<RecruiterNoteResponse>> GetNoteByIdAsync(
        Guid applicationId,
        Guid noteId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<RecruiterNoteResponse>> UpdateNoteAsync(
        Guid applicationId,
        Guid noteId,
        UpdateRecruiterNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteNoteAsync(
        Guid applicationId,
        Guid noteId,
        CancellationToken cancellationToken = default);
}
