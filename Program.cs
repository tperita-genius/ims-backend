using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ims_backend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. 設定 CORS 政策名稱為 AllowAngular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy => 
        policy.WithOrigins("http://localhost:4200", "https://ims-frontend-azure.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// 2. Supabase 連線設定
string supabaseUrl = builder.Configuration["SUPABASE_API_URL"] ?? throw new InvalidOperationException("Missing SupabaseUrl in configuration.");
string supabaseAnonKey = builder.Configuration["SUPABASE_ROLE_SECRET"] ?? throw new InvalidOperationException("Missing supabaseAnonKey in configuration.");

builder.Services.AddHttpClient("Supabase", client =>
{
    client.BaseAddress = new Uri($"{supabaseUrl.TrimEnd('/')}/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", supabaseAnonKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseAnonKey}");
    client.DefaultRequestHeaders.Add("Prefer", "return=representation");
});

// 3. 讀取與註冊 JWT 設定
var jwtKey = builder.Configuration["Jwt:Key"] 
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ims-backend";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ims-frontend";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddScoped<IJwtService, JwtService>();

var app = builder.Build();

// -------------------------------------------------------------
// 中間件 (Middleware) 順序設定區：務必放在所有 Map API 之前！
// -------------------------------------------------------------
app.UseRouting();

// 1. 修正 CORS 名稱與上面 AddPolicy("AllowAngular") 對齊
app.UseCors("AllowAngular"); 

// 2. 身份驗證與授權 (必須放在 UseCors 後面、Map 前面)
app.UseAuthentication(); 
app.UseAuthorization();

// -------------------------------------------------------------
// 轉發 Controller API (包含 /api/auth/register, /api/auth/login)
// -------------------------------------------------------------
app.MapControllers();

// -------------------------------------------------------------
// Minimal APIs (產品 CRUD 端點)
// -------------------------------------------------------------

// GET: 取得產品清單
app.MapGet("/api/products", async (
    IHttpClientFactory httpClientFactory, 
    int page = 1, 
    int limit = 10, 
    string? search = null, 
    string? status = "all") =>
{
    var client = httpClientFactory.CreateClient("Supabase");

    page = Math.Max(1, page);
    limit = Math.Clamp(limit, 1, 100); 
    int offset = (page - 1) * limit;

    var queryParts = new List<string>
    {
        "select=*",
        "order=created_at.desc",
        $"limit={limit}",
        $"offset={offset}"
    };

    if (status == "active")
    {
        queryParts.Add("is_active=eq.true");
    }
    else if (status == "inactive")
    {
        queryParts.Add("is_active=eq.false");
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var safeSearch = search.Replace("(", "").Replace(")", "").Replace(",", "").Trim();
        if (!string.IsNullOrEmpty(safeSearch))
        {
            var encodedSearch = Uri.EscapeDataString(safeSearch);
            queryParts.Add($"or=(title.ilike.*{encodedSearch}*,description.ilike.*{encodedSearch}*)");
        }
    }

    string requestUrl = "products?" + string.Join("&", queryParts);

    var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
    request.Headers.Add("Prefer", "count=exact");

    var response = await client.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
        var errorMsg = await response.Content.ReadAsStringAsync();
        return Results.Problem($"資料庫讀取失敗: {errorMsg}");
    }

    var products = await response.Content.ReadFromJsonAsync<List<Product>>();

    int totalCount = 0;
    if (response.Content.Headers.TryGetValues("Content-Range", out var ranges))
    {
        var rangeStr = ranges.FirstOrDefault();
        if (rangeStr != null && rangeStr.Contains("/"))
        {
            int.TryParse(rangeStr.Split('/')[1], out totalCount);
        }
    }

    return Results.Ok(new 
    {
        totalCount = totalCount,
        page = page,
        pageSize = limit,
        data = products ?? new List<Product>()
    });
});

// POST: 新增產品
app.MapPost("/api/products", async (Product dto, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("Supabase");
    
    var newProduct = new 
    {
        title = dto.Title,
        description = dto.Description,
        price = dto.Price,
        is_active = dto.IsActive
    };

    var response = await client.PostAsJsonAsync("products", newProduct);

    if (!response.IsSuccessStatusCode)
        return Results.Problem($"新增失敗: {await response.Content.ReadAsStringAsync()}");

    var createdProduct = await response.Content.ReadFromJsonAsync<List<Product>>();
    return Results.Created($"/api/products/{createdProduct?[0].Id}", createdProduct?[0]);
});

// PUT: 修改產品
app.MapPut("/api/products/{id}", async (string id, Product dto, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("Supabase");
    
    var updateProduct = new 
    {
        title = dto.Title,
        description = dto.Description,
        price = dto.Price,
        is_active = dto.IsActive
    };

    var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"products?id=eq.{id}")
    {
        Content = JsonContent.Create(updateProduct)
    };
    request.Headers.Add("Prefer", "return=representation");

    var response = await client.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
        var errorMsg = await response.Content.ReadAsStringAsync();
        return Results.Problem($"更新失敗: {errorMsg}");
    }

    var updatedProducts = await response.Content.ReadFromJsonAsync<List<Product>>();

    if (updatedProducts == null || updatedProducts.Count == 0)
    {
        return Results.NotFound($"找不到 ID 為 '{id}' 的產品，或是資料未進行變更");
    }

    return Results.Ok(updatedProducts[0]);
});

// DELETE: 刪除產品
app.MapDelete("/api/products/{id}", async (string id, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("Supabase");
    var response = await client.DeleteAsync($"products?id=eq.{id}");

    if (!response.IsSuccessStatusCode)
    {
        var errorMsg = await response.Content.ReadAsStringAsync();
        return Results.Problem($"刪除失敗: {errorMsg}");
    }

    return Results.Ok(new { message = "刪除成功", id });
});

app.Run();

// 產品 Model
public class Product
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}