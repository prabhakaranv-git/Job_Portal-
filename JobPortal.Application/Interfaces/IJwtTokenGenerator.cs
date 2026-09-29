using JobPortal.Domain.Entities;

namespace JobPortal.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
    (RefreshToken Entity, string RawToken) GenerateRefreshToken(Guid userId);
    string HashToken(string rawToken);
}
