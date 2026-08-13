using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. 設定 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

// 2. Supabase 連線設定（請替換為你的實際資料）
string supabaseUrl = "https://yyvgthfzjyntvvhnbjjw.supabase.co"; 
string supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inl5dmd0aGZ6anludHZ2aG5iamp3Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODY1MjMxMjgsImV4cCI6MjEwMjA5OTEyOH0.vlIysGwNcPScbX0-CGd3C-Pbsz1CVuKi4CI_O2BsB1E";

builder.Services.AddHttpClient("Supabase", client =>
{
    client.BaseAddress = new Uri($"{supabaseUrl.TrimEnd('/')}/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", supabaseAnonKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseAnonKey}");
    // 讓 Supabase 回傳新增/更新後的完整物件資料
    client.DefaultRequestHeaders.Add("Prefer", "return=representation");
});

var app = builder.Build();
app.UseCors("AllowAngular");

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
    
    // 忽略前端傳入的 Id，由資料庫自動產生
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
    
    // 建立要更新的欄位 JSON 物件
    var updateProduct = new 
    {
        title = dto.Title,
        description = dto.Description,
        price = dto.Price,
        is_active = dto.IsActive
    };

    // 💡 建立單次請求標頭，明確要求 Supabase 回傳修改後的完整資料 (return=representation)
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

    // 💡【關鍵防呆修正】檢查陣列是否為空，避免 IndexOutOfRangeException / ArgumentOutOfRangeException 崩潰
    if (updatedProducts == null || updatedProducts.Count == 0)
    {
        // 若找不到該 ID，回傳 404 Not Found 或直接回傳傳入的 dto
        return Results.NotFound($"找不到 ID 為 '{id}' 的產品，或是資料未進行變更");
    }

    return Results.Ok(updatedProducts[0]);
});

// DELETE: 刪除產品
app.MapDelete("/api/products/{id}", async (string id, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("Supabase");

    // 發送 DELETE 請求給 Supabase REST API (過濾條件：id 等於傳入的 id)
    var response = await client.DeleteAsync($"products?id=eq.{id}");

    if (!response.IsSuccessStatusCode)
    {
        var errorMsg = await response.Content.ReadAsStringAsync();
        return Results.Problem($"刪除失敗: {errorMsg}");
    }

    return Results.Ok(new { message = "刪除成功", id });
});

app.Run("http://localhost:5035");

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