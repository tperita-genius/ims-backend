using Xunit;
using BCrypt.Net;

namespace ims_backend.Tests;

public class AuthTests
{
    [Fact]
    public void PasswordHash_Should_Hash_And_Verify_Correctly()
    {
        // Arrange
        string rawPassword = "SecurePassword123";

        // Act
        string hash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
        bool isValid = BCrypt.Net.BCrypt.Verify(rawPassword, hash);
        bool isInvalidPasswordValid = BCrypt.Net.BCrypt.Verify("WrongPassword", hash);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash);
        Assert.True(isValid);
        Assert.False(isInvalidPasswordValid);
    }
}