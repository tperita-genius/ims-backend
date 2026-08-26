using System.Text.Json;
using ims_backend.Models;

namespace ims_backend.Services;

public interface IProductsService
{
    Task<(int totalCount, List<Product> data)> GetProductsAsync(int page, int limit, string? search, string? status);
    Task<Product?> CreateProductAsync(Product dto);
    Task<Product?> UpdateProductAsync(string id, Product dto);
    Task<bool> DeleteProductAsync(string id);
}

public class ProductsService : IProductsService
{
    private readonly HttpClient _http;

    public ProductsService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("Supabase");
    }

    public async Task<(int totalCount, List<Product> data)> GetProductsAsync(int page, int limit, string? search, string? status)
    {
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

        if (status == "active") queryParts.Add("is_active=eq.true");
        else if (status == "inactive") queryParts.Add("is_active=eq.false");

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

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = await response.Content.ReadAsStringAsync();
            throw new Exception(errorMsg);
        }

        var products = await response.Content.ReadFromJsonAsync<List<Product>>() ?? new List<Product>();

        int totalCount = 0;
        if (response.Content.Headers.TryGetValues("Content-Range", out var ranges))
        {
            var rangeStr = ranges.FirstOrDefault();
            if (rangeStr != null && rangeStr.Contains("/"))
            {
                int.TryParse(rangeStr.Split('/')[1], out totalCount);
            }
        }

        return (totalCount, products);
    }

    public async Task<Product?> CreateProductAsync(Product dto)
    {
        var newProduct = new
        {
            title = dto.Title,
            description = dto.Description,
            price = dto.Price,
            is_active = dto.IsActive
        };

        var response = await _http.PostAsJsonAsync("products", newProduct);
        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = await response.Content.ReadAsStringAsync();
            throw new Exception(errorMsg);
        }

        var createdProducts = await response.Content.ReadFromJsonAsync<List<Product>>();
        return createdProducts?.FirstOrDefault();
    }

    public async Task<Product?> UpdateProductAsync(string id, Product dto)
    {
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

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = await response.Content.ReadAsStringAsync();
            throw new Exception(errorMsg);
        }

        var updatedProducts = await response.Content.ReadFromJsonAsync<List<Product>>();
        return updatedProducts?.FirstOrDefault();
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        var response = await _http.DeleteAsync($"products?id=eq.{id}");
        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = await response.Content.ReadAsStringAsync();
            throw new Exception(errorMsg);
        }
        return true;
    }
}