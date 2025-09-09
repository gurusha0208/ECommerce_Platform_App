using Catalog.API.Application.DTOs;
using Catalog.API.Domain.Entities;
using Catalog.API.Infrastructure.Data;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Application.Services
{
    public interface ICategoryService
    {
        Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync();
        Task<ApiResponse<CategoryDto?>> GetCategoryByIdAsync(int id);
        Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto createCategoryDto);
        Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(int id, UpdateCategoryDto updateCategoryDto);
        Task<ApiResponse<bool>> DeleteCategoryAsync(int id);
        Task<ApiResponse<List<CategoryDto>>> GetCategoryHierarchyAsync();
    }

    public class CategoryService : ICategoryService
    {
        private readonly CatalogDbContext _context;

        public CategoryService(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync()
        {
            try
            {
                var categories = await _context.Categories
                    .Include(c => c.Children)
                    .Where(c => c.ParentId == null) // Get root categories
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var categoryDtos = categories.Select(MapToCategoryDto).ToList();

                return ApiResponse<List<CategoryDto>>.SuccessResult(categoryDtos);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CategoryDto>>.ErrorResult($"Error retrieving categories: {ex.Message}");
            }
        }

        public async Task<ApiResponse<CategoryDto?>> GetCategoryByIdAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Children)
                    .Include(c => c.Parent)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return ApiResponse<CategoryDto?>.ErrorResult("Category not found");
                }

                var categoryDto = MapToCategoryDto(category);
                return ApiResponse<CategoryDto?>.SuccessResult(categoryDto);
            }
            catch (Exception ex)
            {
                return ApiResponse<CategoryDto?>.ErrorResult($"Error retrieving category: {ex.Message}");
            }
        }

        public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
            try
            {
                // Validate parent category exists if provided
                if (createCategoryDto.ParentId.HasValue)
                {
                    var parentExists = await _context.Categories
                        .AnyAsync(c => c.Id == createCategoryDto.ParentId.Value);
                    
                    if (!parentExists)
                    {
                        return ApiResponse<CategoryDto>.ErrorResult("Parent category not found");
                    }
                }

                // Check if category name already exists at the same level
                var nameExists = await _context.Categories
                    .AnyAsync(c => c.Name.ToLower() == createCategoryDto.Name.ToLower() 
                                && c.ParentId == createCategoryDto.ParentId);

                if (nameExists)
                {
                    return ApiResponse<CategoryDto>.ErrorResult("Category with this name already exists at this level");
                }

                var category = new Category
                {
                    Name = createCategoryDto.Name,
                    Description = createCategoryDto.Description,
                    ParentId = createCategoryDto.ParentId
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                // Load the created category with relationships
                var createdCategory = await _context.Categories
                    .Include(c => c.Children)
                    .Include(c => c.Parent)
                    .FirstAsync(c => c.Id == category.Id);

                var categoryDto = MapToCategoryDto(createdCategory);

                return ApiResponse<CategoryDto>.SuccessResult(categoryDto, "Category created successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<CategoryDto>.ErrorResult($"Error creating category: {ex.Message}");
            }
        }

        public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(int id, UpdateCategoryDto updateCategoryDto)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Children)
                    .Include(c => c.Parent)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return ApiResponse<CategoryDto>.ErrorResult("Category not found");
                }

                // Validate parent category if changing
                if (updateCategoryDto.ParentId.HasValue && updateCategoryDto.ParentId != category.ParentId)
                {
                    // Check if parent exists
                    var parentExists = await _context.Categories
                        .AnyAsync(c => c.Id == updateCategoryDto.ParentId.Value);
                    
                    if (!parentExists)
                    {
                        return ApiResponse<CategoryDto>.ErrorResult("Parent category not found");
                    }

                    // Prevent circular reference
                    if (updateCategoryDto.ParentId == id)
                    {
                        return ApiResponse<CategoryDto>.ErrorResult("Category cannot be its own parent");
                    }

                    // Check if the new parent is a descendant of current category
                    if (await IsDescendantAsync(id, updateCategoryDto.ParentId.Value))
                    {
                        return ApiResponse<CategoryDto>.ErrorResult("Cannot set a descendant as parent (circular reference)");
                    }
                }

                // Update properties
                if (!string.IsNullOrEmpty(updateCategoryDto.Name))
                {
                    // Check if name already exists at the same level (excluding current category)
                    var nameExists = await _context.Categories
                        .AnyAsync(c => c.Name.ToLower() == updateCategoryDto.Name.ToLower() 
                                    && c.ParentId == (updateCategoryDto.ParentId ?? category.ParentId)
                                    && c.Id != id);

                    if (nameExists)
                    {
                        return ApiResponse<CategoryDto>.ErrorResult("Category with this name already exists at this level");
                    }

                    category.Name = updateCategoryDto.Name;
                }

                if (!string.IsNullOrEmpty(updateCategoryDto.Description))
                {
                    category.Description = updateCategoryDto.Description;
                }

                if (updateCategoryDto.ParentId != category.ParentId)
                {
                    category.ParentId = updateCategoryDto.ParentId;
                }

                category.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Reload with relationships
                await _context.Entry(category)
                    .Collection(c => c.Children)
                    .LoadAsync();
                
                if (category.ParentId.HasValue)
                {
                    await _context.Entry(category)
                        .Reference(c => c.Parent)
                        .LoadAsync();
                }

                var categoryDto = MapToCategoryDto(category);

                return ApiResponse<CategoryDto>.SuccessResult(categoryDto, "Category updated successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<CategoryDto>.ErrorResult($"Error updating category: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteCategoryAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Children)
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return ApiResponse<bool>.ErrorResult("Category not found");
                }

                // Check if category has products
                if (category.Products.Any())
                {
                    return ApiResponse<bool>.ErrorResult("Cannot delete category with associated products");
                }

                // Check if category has children
                if (category.Children.Any())
                {
                    return ApiResponse<bool>.ErrorResult("Cannot delete category with subcategories");
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.SuccessResult(true, "Category deleted successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult($"Error deleting category: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<CategoryDto>>> GetCategoryHierarchyAsync()
        {
            try
            {
                var allCategories = await _context.Categories
                    .Include(c => c.Children)
                    .ToListAsync();

                var rootCategories = allCategories
                    .Where(c => c.ParentId == null)
                    .OrderBy(c => c.Name)
                    .ToList();

                var categoryDtos = rootCategories.Select(category => MapToCategoryDtoWithHierarchy(category, allCategories)).ToList();

                return ApiResponse<List<CategoryDto>>.SuccessResult(categoryDtos);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CategoryDto>>.ErrorResult($"Error retrieving category hierarchy: {ex.Message}");
            }
        }

        private CategoryDto MapToCategoryDto(Category category)
        {
            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentId = category.ParentId,
                Children = category.Children?.Select(MapToCategoryDto).ToList() ?? new List<CategoryDto>()
            };
        }

        private CategoryDto MapToCategoryDtoWithHierarchy(Category category, List<Category> allCategories)
        {
            var children = allCategories
                .Where(c => c.ParentId == category.Id)
                .OrderBy(c => c.Name)
                .Select(child => MapToCategoryDtoWithHierarchy(child, allCategories))
                .ToList();

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentId = category.ParentId,
                Children = children
            };
        }

        private async Task<bool> IsDescendantAsync(int ancestorId, int potentialDescendantId)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == potentialDescendantId);

            while (category?.ParentId != null)
            {
                if (category.ParentId == ancestorId)
                    return true;

                category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == category.ParentId);
            }

            return false;
        }
    }
}