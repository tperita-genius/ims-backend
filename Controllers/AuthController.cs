using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using ims_backend.DTOs;
using ims_backend.Models;
using ims_backend.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace ims_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly IJwtService _jwtService;

    // 注入 Supabase HttpClient 與 JwtService
    public AuthController(IHttpClientFactory httpClientFactory, IJwtService jwtService)
    {
        _http = httpClientFactory.CreateClient("Supabase");
        _jwtService = jwtService;
    }

    /// <summary>
    /// [POST] api/auth/register - 會員註冊
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // 1. 自動觸發 Model 欄位驗證
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 2. 檢查 Email 是否已存在於 Supabase users 資料表中
        // 查詢語法: GET /users?email=eq.user@example.com
        var checkEmailResponse = await _http.GetAsync($"users?email=eq.{Uri.EscapeDataString(dto.Email)}");
        if (checkEmailResponse.IsSuccessStatusCode)
        {
            var existingUsersJson = await checkEmailResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(existingUsersJson);
            
            // 若傳回的 JSON 陣列長度大於 0，代表此 Email 已被註冊過
            if (doc.RootElement.GetArrayLength() > 0)
            {
                return BadRequest(new { message = "此 Email 已被註冊，請使用其他 Email 或直接登入" });
            }
        }

        // 3. 使用 BCrypt 對密碼進行安全雜湊加密
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // 4. 建立新會員物件
        var newUser = new
        {
            id = Guid.NewGuid(),
            email = dto.Email,
            password_hash = passwordHash,
            full_name = dto.FullName,
            role = "User", // 預設新註冊會員權限為 User
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };

        // 5. 透過 HTTP POST 寫入 Supabase REST API (POST /users)
        var content = new StringContent(
            JsonSerializer.Serialize(newUser),
            Encoding.UTF8,
            "application/json"
        );

        var insertResponse = await _http.PostAsync("users", content);

        if (!insertResponse.IsSuccessStatusCode)
        {
            var errorDetail = await insertResponse.Content.ReadAsStringAsync();
            return StatusCode((int)insertResponse.StatusCode, new { message = "帳號建立失敗，請稍後再試", detail = errorDetail });
        }

        return Ok(new { message = "註冊成功！請前往登入頁面進行登入" });
    }

    /// <summary>
    /// [POST] api/auth/login - 會員登入 (搭配 Supabase 查詢)
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // 1. 依 Email 查詢 Supabase users 資料表
        var response = await _http.GetAsync($"users?email=eq.{Uri.EscapeDataString(dto.Email)}");
        
        if (!response.IsSuccessStatusCode)
        {
            return Unauthorized(new { message = "帳號或密碼錯誤" });
        }

        var json = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<List<User>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var user = users?.FirstOrDefault();

        // 帳號不存在
        if (user == null)
        {
            return Unauthorized(new { message = "帳號或密碼錯誤" });
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !user.PasswordHash.StartsWith("$2"))
        {
            return Unauthorized(new { message = "帳號密碼資料異常，請聯繫管理員或重新註冊" });
        }

        // 帳號已被停用
        if (!user.IsActive)
        {
            return Unauthorized(new { message = "此帳號已被停用，請聯繫系統管理者" });
        }

        // 4. 比對密碼雜湊值
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Unauthorized(new { message = "帳號或密碼錯誤" });
        }

        // 5. 驗證成功，簽發 JWT Token
        string token = _jwtService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            Token: token,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role
        ));
    }
}