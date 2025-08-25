using Catalog.API.Application.DTOs;
using Catalog.API.Domain.Entities;
using Catalog.API.Infrastructure.Data;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Application.Services
{
    public interface IProductService
    {
        Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(SearchCriteria criteria);
        Task<ApiResponse<ProductDto?>> GetProductByIdAsync(int id);
        Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto createProductDto);
        Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
        Task<ApiResponse<bool>> DeleteProductAsync(int id);
    }

    public class ProductService : IProductService
    {
        private readonly CatalogDbContext _context;

        public ProductService(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(SearchCriteria criteria)
        {
            try
            {
                var query = _context.Products.Include(p => p.Category).AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(criteria.SearchTerm))
                {
                    query = query.Where(p => p.Name.Contains(criteria.SearchTerm) || 
                                           p.Description.Contains(criteria.SearchTerm));
                }

                if (criteria.CategoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == criteria.CategoryId.Value);
                }

                if (criteria.MinPrice.HasValue)
                {
                    query = query.Where(p => p.Price >= criteria.MinPrice.Value);
                }

                if (criteria.MaxPrice.HasValue)
                {
                    query = query.Where(p => p.Price <= criteria.MaxPrice.Value);
                }

                // Apply sorting
                query = criteria.SortBy.ToLower() switch
                {
                    "price" => criteria.SortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                    "name" => criteria.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                    "created" => criteria.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                    _ => query.OrderBy(p => p.Name)
                };

                var totalCount = await query.CountAsync();

                var products = await query
                    .Skip((criteria.PageNumber - 1) * criteria.PageSize)
                    .Take(criteria.PageSize)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Price = p.Price,
                        SKU = p.SKU,
                        CategoryId = p.CategoryId,
                        CategoryName = p.Category.Name,
                        Status = p.Status.ToString(),
                        ImageUrl = p.ImageUrl,
                        StockQuantity = p.StockQuantity,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                var result = new PagedResult<ProductDto>
                {
                    Items = products,
                    TotalCount = totalCount,
                    PageNumber = criteria.PageNumber,
                    PageSize = criteria.PageSize
                };

                return ApiResponse<PagedResult<ProductDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<ProductDto>>.ErrorResult($"Error retrieving products: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ProductDto?>> GetProductByIdAsync(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return ApiResponse<ProductDto?>.ErrorResult("Product not found");
                }

                var productDto = new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    SKU = product.SKU,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category.Name,
                    Status = product.Status.ToString(),
                    ImageUrl = product.ImageUrl,
                    StockQuantity = product.StockQuantity,
                    CreatedAt = product.CreatedAt
                };

                return ApiResponse<ProductDto?>.SuccessResult(productDto);
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto?>.ErrorResult($"Error retrieving product: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto createProductDto)
        {
            try
            {
                // Check if category exists
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == createProductDto.CategoryId);
                if (!categoryExists)
                {
                    return ApiResponse<ProductDto>.ErrorResult("Category not found");
                }

                // Check if SKU is unique
                var skuExists = await _context.Products.AnyAsync(p => p.SKU == createProductDto.SKU);
                if (skuExists)
                {
                    return ApiResponse<ProductDto>.ErrorResult("SKU already exists");
                }

                var product = new Product(
                    createProductDto.Name,
                    createProductDto.Description,
                    createProductDto.Price,
                    createProductDto.SKU,
                    createProductDto.CategoryId,
                    createProductDto.ImageUrl,
                    createProductDto.StockQuantity
                );

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Load category for response
                await _context.Entry(product)
                    .Reference(p => p.Category)
                    .LoadAsync();

                var productDto = new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    SKU = product.SKU,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category.Name,
                    Status = product.Status.ToString(),
                    ImageUrl = product.ImageUrl,
                    StockQuantity = product.StockQuantity,
                    CreatedAt = product.CreatedAt
                };

                return ApiResponse<ProductDto>.SuccessResult(productDto, "Product created successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.ErrorResult($"Error creating product: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto updateProductDto)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return ApiResponse<ProductDto>.ErrorResult("Product not found");
                }

                // Update properties if provided
                if (!string.IsNullOrEmpty(updateProductDto.Name))
                    product.GetType().GetProperty("Name")?.SetValue(product, updateProductDto.Name);

                if (!string.IsNullOrEmpty(updateProductDto.Description))
                    product.GetType().GetProperty("Description")?.SetValue(product, updateProductDto.Description);

                if (updateProductDto.Price.HasValue)
                    product.UpdatePrice(updateProductDto.Price.Value);

                if (!string.IsNullOrEmpty(updateProductDto.ImageUrl))
                    product.GetType().GetProperty("ImageUrl")?.SetValue(product, updateProductDto.ImageUrl);

                if (updateProductDto.StockQuantity.HasValue)
                    product.UpdateStock(updateProductDto.StockQuantity.Value);

                await _context.SaveChangesAsync();

                var productDto = new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    SKU = product.SKU,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category.Name,
                    Status = product.Status.ToString(),
                    ImageUrl = product.ImageUrl,
                    StockQuantity = product.StockQuantity,
                    CreatedAt = product.CreatedAt
                };

                return ApiResponse<ProductDto>.SuccessResult(productDto, "Product updated successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.ErrorResult($"Error updating product: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteProductAsync(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return ApiResponse<bool>.ErrorResult("Product not found");
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.SuccessResult(true, "Product deleted successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult($"Error deleting product: {ex.Message}");
            }
        }
    }
}