using System.ComponentModel.DataAnnotations;

namespace ims_backend.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "請輸入姓名")]
    [MaxLength(100, ErrorMessage = "姓名長度不能超過 100 字元")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入 Email")]
    [EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
    [MaxLength(255, ErrorMessage = "Email 長度不能超過 255 字元")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼")]
    [MinLength(6, ErrorMessage = "密碼長度至少需要 6 個字元")]
    public string Password { get; set; } = string.Empty;
}
public record LoginDto(
    [property: Required(ErrorMessage = "電子郵件為必填")]
    [property: EmailAddress(ErrorMessage = "電子郵件格式不正確")]
    string Email,

    [property: Required(ErrorMessage = "密碼為必填")]
    [property: MinLength(6, ErrorMessage = "密碼長度至少需 6 碼")]
    string Password
);
public record AuthResponseDto(string Token, string Email, string FullName, string Role);
public record UserResponseDto(Guid Id, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAt);

public record ToggleStatusDto(
    bool IsActive
);