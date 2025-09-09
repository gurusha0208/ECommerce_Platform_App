using Basket.API.DTOs;
using Basket.API.Models;
using Common.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace Basket.API.Services
{
    public interface IBasketService
    {
        Task<ApiResponse<CartDto?>> GetBasketAsync(int userId);
        Task<ApiResponse<CartDto>> AddToBasketAsync(int userId, AddToCartDto addToCartDto);
        Task<ApiResponse<CartDto>> UpdateBasketItemAsync(int userId, UpdateCartItemDto updateCartItemDto);
        Task<ApiResponse<bool>> RemoveFromBasketAsync(int userId, int productId);
        Task<ApiResponse<bool>> ClearBasketAsync(int userId);
    }

    public class BasketService : IBasketService
    {
        private readonly IDatabase _database;
        private readonly IHttpClientFactory _httpClientFactory;

        public BasketService(IConnectionMultiplexer redis, IHttpClientFactory httpClientFactory)
        {
            _database = redis.GetDatabase();
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ApiResponse<CartDto?>> GetBasketAsync(int userId)
        {
            try
            {
                var basketJson = await _database.StringGetAsync($"basket:{userId}");
                if (!basketJson.HasValue)
                {
                    return ApiResponse<CartDto?>.SuccessResult(null);
                }

                var basket = JsonSerializer.Deserialize<ShoppingCart>(basketJson);
                var cartDto = MapToCartDto(basket!);

                return ApiResponse<CartDto?>.SuccessResult(cartDto);
            }
            catch (Exception ex)
            {
                return ApiResponse<CartDto?>.ErrorResult($"Error retrieving basket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<CartDto>> AddToBasketAsync(int userId, AddToCartDto addToCartDto)
        {
            try
            {
                // Get product details from Catalog API
                var httpClient = _httpClientFactory.CreateClient("CatalogAPI");
                var productResponse = await httpClient.GetAsync($"products/{addToCartDto.ProductId}");
                
                if (!productResponse.IsSuccessStatusCode)
                {
                    return ApiResponse<CartDto>.ErrorResult("Product not found");
                }

                var productJson = await productResponse.Content.ReadAsStringAsync();
                var productApiResponse = JsonSerializer.Deserialize<ApiResponse<ProductDto>>(productJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (!productApiResponse.Success || productApiResponse.Data == null)
                {
                    return ApiResponse<CartDto>.ErrorResult("Product not found");
                }

                var product = productApiResponse.Data;

                // Get existing basket or create new one
                var basketJson = await _database.StringGetAsync($"basket:{userId}");
                var basket = basketJson.HasValue 
                    ? JsonSerializer.Deserialize<ShoppingCart>(basketJson)
                    : new ShoppingCart { UserId = userId };

                // Add item to basket
                basket!.AddItem(product.Id, product.Name, product.Price, addToCartDto.Quantity, product.ImageUrl);

                // Save basket
                var updatedBasketJson = JsonSerializer.Serialize(basket);
                await _database.StringSetAsync($"basket:{userId}", updatedBasketJson, TimeSpan.FromDays(30));

                var cartDto = MapToCartDto(basket);
                return ApiResponse<CartDto>.SuccessResult(cartDto, "Item added to cart");
            }
            catch (Exception ex)
            {
                return ApiResponse<CartDto>.ErrorResult($"Error adding to basket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<CartDto>> UpdateBasketItemAsync(int userId, UpdateCartItemDto updateCartItemDto)
        {
            try
            {
                var basketJson = await _database.StringGetAsync($"basket:{userId}");
                if (!basketJson.HasValue)
                {
                    return ApiResponse<CartDto>.ErrorResult("Basket not found");
                }

                var basket = JsonSerializer.Deserialize<ShoppingCart>(basketJson);
                basket!.UpdateItemQuantity(updateCartItemDto.ProductId, updateCartItemDto.Quantity);

                var updatedBasketJson = JsonSerializer.Serialize(basket);
                await _database.StringSetAsync($"basket:{userId}", updatedBasketJson, TimeSpan.FromDays(30));

                var cartDto = MapToCartDto(basket);
                return ApiResponse<CartDto>.SuccessResult(cartDto, "Cart updated");
            }
            catch (Exception ex)
            {
                return ApiResponse<CartDto>.ErrorResult($"Error updating basket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> RemoveFromBasketAsync(int userId, int productId)
        {
            try
            {
                var basketJson = await _database.StringGetAsync($"basket:{userId}");
                if (!basketJson.HasValue)
                {
                    return ApiResponse<bool>.ErrorResult("Basket not found");
                }

                var basket = JsonSerializer.Deserialize<ShoppingCart>(basketJson);
                basket!.RemoveItem(productId);

                if (basket.Items.Count == 0)
                {
                    await _database.KeyDeleteAsync($"basket:{userId}");
                }
                else
                {
                    var updatedBasketJson = JsonSerializer.Serialize(basket);
                    await _database.StringSetAsync($"basket:{userId}", updatedBasketJson, TimeSpan.FromDays(30));
                }

                return ApiResponse<bool>.SuccessResult(true, "Item removed from cart");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult($"Error removing from basket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> ClearBasketAsync(int userId)
        {
            try
            {
                await _database.KeyDeleteAsync($"basket:{userId}");
                return ApiResponse<bool>.SuccessResult(true, "Cart cleared");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult($"Error clearing basket: {ex.Message}");
            }
        }

        private CartDto MapToCartDto(ShoppingCart basket)
        {
            return new CartDto
            {
                Id = basket.Id,
                UserId = basket.UserId,
                Items = basket.Items.Select(item => new CartItemDto
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    ImageUrl = item.ImageUrl,
                    TotalPrice = item.TotalPrice,
                    AddedAt = item.AddedAt
                }).ToList(),
                TotalAmount = basket.TotalAmount,
                TotalItems = basket.TotalItems,
                UpdatedAt = basket.UpdatedAt
            };
        }
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
    }
}