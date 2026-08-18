using Xunit;
using BCrypt.Net;

namespace ims_backend.Tests;

public class AuthLogicTests
{
    [Theory]
    [InlineData("Admin@12345")]
    [InlineData("Pass_Word_999")]
    [InlineData("aVeryComplex!Password#2026")]
    public void HashPassword_Should_Generate_Valid_BCrypt_Hash(string plainPassword)
    {
        // Act
        string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash); // BCrypt 標準開頭標記
        Assert.True(BCrypt.Net.BCrypt.Verify(plainPassword, hash));
    }

    [Fact]
    public void Verify_With_Wrong_Password_Should_Return_False()
    {
        // Arrange
        string plainPassword = "CorrectPassword123";
        string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

        // Act
        bool result = BCrypt.Net.BCrypt.Verify("WrongPassword456", hash);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_With_Null_Or_Empty_Hash_Should_Throw_ArgumentException(string? invalidHash)
    {
        // 改用 ThrowsAny，可同時相容 ArgumentException 與 ArgumentNullException
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            BCrypt.Net.BCrypt.Verify("AnyPassword", invalidHash);
        });
    }
}