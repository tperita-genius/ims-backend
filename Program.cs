using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
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
string supabaseAnonKey = builder.Configuration["SUPABASE_SECRET_KEY"] ?? throw new InvalidOperationException("Missing supabaseAnonKey in configuration.");

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

// 4. 註冊服務 (DI 容器)
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IProductsService, ProductsService>(); // ✅ 註冊新的 ProductsService

builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("LoginPolicy", opt => {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; // 同一個 IP 一分鐘只能嘗試登入 5 次
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

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
app.UseRateLimiter();

// -------------------------------------------------------------
// 轉發 Controller API (Auth, Users, Products 全部從這裡進去)
// -------------------------------------------------------------
app.MapControllers();

app.Run();

public partial class Program {}