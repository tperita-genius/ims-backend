using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ims_backend.DTOs;
using ims_backend.Models;

namespace ims_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // 需攜帶有效 JWT Token
public class UsersController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions = new() 
{ 
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true 
};

    public UsersController(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("Supabase");
    }

    /// <summary>
    /// [GET] api/users - 取得所有會員清單 (安全不傳回密碼雜湊)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? search)
    {
        // 僅撈取公開安全的欄位，排除 password_hash
        string endpoint = "users?select=id,email,full_name,role,is_active,created_at&order=created_at.desc";
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            endpoint += $"&or=(full_name.ilike.*{Uri.EscapeDataString(search)}*,email.ilike.*{Uri.EscapeDataString(search)}*)";
        }

        var response = await _http.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "無法取得會員資料");

        var json = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<List<UserResponseDto>>(json, _jsonOptions);
        return Ok(users);
    }

    /// <summary>
    /// [PATCH] api/users/{id}/status - 切換會員啟用/停用狀態
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] ToggleStatusDto dto)
    {
        var updateData = new
        {
            is_active = dto.IsActive,
            updated_at = DateTime.UtcNow
        };

        var content = new StringContent(JsonSerializer.Serialize(updateData), Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync($"users?id=eq.{id}", content);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "狀態更新失敗");

        return NoContent();
    }

    /// <summary>
    /// [DELETE] api/users/{id} - 刪除會員
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")] // 🔒 可設定僅限 Admin 角色執行刪除操作
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var response = await _http.DeleteAsync($"users?id=eq.{id}");
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "刪除會員失敗");

        return NoContent();
    }
}