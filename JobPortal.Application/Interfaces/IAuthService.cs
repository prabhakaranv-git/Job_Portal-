using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;

namespace JobPortal.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse> RevokeTokenAsync(RevokeTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default);
}
