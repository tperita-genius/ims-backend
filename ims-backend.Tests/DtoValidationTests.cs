using System.ComponentModel.DataAnnotations;
using Xunit;
using ims_backend.DTOs;

namespace ims_backend.Tests;

public class DtoValidationTests
{
    private IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void LoginDto_Should_Fail_When_Email_Is_Empty()
    {
        // 修正：使用建構函式傳入參數，而非 { Email = ... }
        var dto = new LoginDto("", "ValidPassword123");

        // Act
        var errors = ValidateModel(dto);

        // Assert
        Assert.NotEmpty(errors);
    }
}