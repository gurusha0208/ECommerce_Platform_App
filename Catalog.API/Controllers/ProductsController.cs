using Catalog.API.Application.DTOs;
using Catalog.API.Application.Services;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetProducts([FromQuery] SearchCriteria criteria)
        {
            var result = await _productService.GetProductsAsync(criteria);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProductDto?>>> GetProduct(int id)
        {
            var result = await _productService.GetProductByIdAsync(id);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct(CreateProductDto createProductDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<ProductDto>.ErrorResult("Validation failed", errors));
            }

            var result = await _productService.CreateProductAsync(createProductDto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(nameof(GetProduct), new { id = result.Data!.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, UpdateProductDto updateProductDto)
        {
            var result = await _productService.UpdateProductAsync(id, updateProductDto);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
        {
            var result = await _productService.DeleteProductAsync(id);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
    }
}