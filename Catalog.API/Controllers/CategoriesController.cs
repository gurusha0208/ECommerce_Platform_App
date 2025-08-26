using Catalog.API.Application.DTOs;
using Catalog.API.Application.Services;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Get all root categories with their immediate children
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories()
        {
            var result = await _categoryService.GetCategoriesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get complete category hierarchy
        /// </summary>
        [HttpGet("hierarchy")]
        public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategoryHierarchy()
        {
            var result = await _categoryService.GetCategoryHierarchyAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get a specific category by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<CategoryDto?>>> GetCategory(int id)
        {
            var result = await _categoryService.GetCategoryByIdAsync(id);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Create a new category
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory(CreateCategoryDto createCategoryDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<CategoryDto>.ErrorResult("Validation failed", errors));
            }

            var result = await _categoryService.CreateCategoryAsync(createCategoryDto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(nameof(GetCategory), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Update an existing category
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(int id, UpdateCategoryDto updateCategoryDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<CategoryDto>.ErrorResult("Validation failed", errors));
            }

            var result = await _categoryService.UpdateCategoryAsync(id, updateCategoryDto);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete a category
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCategory(int id)
        {
            var result = await _categoryService.DeleteCategoryAsync(id);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
    }
}