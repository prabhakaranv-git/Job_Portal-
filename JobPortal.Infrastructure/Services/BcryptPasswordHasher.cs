using JobPortal.Application.Interfaces;

namespace JobPortal.Infrastructure.Services;

public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
