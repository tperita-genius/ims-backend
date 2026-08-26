using Microsoft.AspNetCore.Mvc;
using ims_backend.Models;
using ims_backend.Services;

namespace ims_backend.Controllers;

[ApiController]
[Route("api/products")] // 🔒 強制綁定路由，絕對不會 404
public class ProductsController : ControllerBase
{
    private readonly IProductsService _productsService;

    public ProductsController(IProductsService productsService)
    {
        _productsService = productsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1, 
        [FromQuery] int limit = 10, 
        [FromQuery] string? search = null, 
        [FromQuery] string? status = "all")
    {
        try
        {
            var (totalCount, data) = await _productsService.GetProductsAsync(page, limit, search, status);
            return Ok(new 
            {
                totalCount = totalCount,
                page = page,
                pageSize = limit,
                data = data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"資料庫讀取失敗: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] Product dto)
    {
        try
        {
            var created = await _productsService.CreateProductAsync(dto);
            if (created == null) return BadRequest("新增失敗");

            return Created($"/api/products/{created.Id}", created);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"新增失敗: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] Product dto)
    {
        try
        {
            var updated = await _productsService.UpdateProductAsync(id, dto);
            if (updated == null) return NotFound($"找不到 ID 為 '{id}' 的產品，或是資料未進行變更");

            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"更新失敗: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        try
        {
            await _productsService.DeleteProductAsync(id);
            return Ok(new { message = "刪除成功", id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"刪除失敗: {ex.Message}");
        }
    }
}