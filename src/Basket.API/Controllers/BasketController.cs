using Basket.API.DTOs;
using Basket.API.Services;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Basket.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BasketController : ControllerBase
    {
        private readonly IBasketService _basketService;
        private readonly ILogger<BasketController> _logger;

        public BasketController(IBasketService basketService, ILogger<BasketController> logger)
        {
            _basketService = basketService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<CartDto?>>> GetBasket()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<CartDto?>.ErrorResult("User not authenticated"));
                }

                var result = await _basketService.GetBasketAsync(userId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving basket for user");
                return StatusCode(500, ApiResponse<CartDto?>.ErrorResult("Internal server error"));
            }
        }

        [HttpPost("items")]
        public async Task<ActionResult<ApiResponse<CartDto>>> AddToBasket([FromBody] AddToCartDto addToCartDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<CartDto>.ErrorResult("Validation failed", errors));
                }

                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<CartDto>.ErrorResult("User not authenticated"));
                }

                if (addToCartDto.Quantity <= 0)
                {
                    return BadRequest(ApiResponse<CartDto>.ErrorResult("Quantity must be positive"));
                }

                var result = await _basketService.AddToBasketAsync(userId.Value, addToCartDto);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to basket for user");
                return StatusCode(500, ApiResponse<CartDto>.ErrorResult("Internal server error"));
            }
        }

        [HttpPut("items")]
        public async Task<ActionResult<ApiResponse<CartDto>>> UpdateBasketItem([FromBody] UpdateCartItemDto updateCartItemDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<CartDto>.ErrorResult("Validation failed", errors));
                }

                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<CartDto>.ErrorResult("User not authenticated"));
                }

                if (updateCartItemDto.Quantity < 0)
                {
                    return BadRequest(ApiResponse<CartDto>.ErrorResult("Quantity cannot be negative"));
                }

                var result = await _basketService.UpdateBasketItemAsync(userId.Value, updateCartItemDto);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating basket item for user");
                return StatusCode(500, ApiResponse<CartDto>.ErrorResult("Internal server error"));
            }
        }

        [HttpDelete("items/{productId}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveFromBasket(int productId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<bool>.ErrorResult("User not authenticated"));
                }

                if (productId <= 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResult("Invalid product ID"));
                }

                var result = await _basketService.RemoveFromBasketAsync(userId.Value, productId);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from basket for user");
                return StatusCode(500, ApiResponse<bool>.ErrorResult("Internal server error"));
            }
        }

        [HttpDelete("clear")]
        public async Task<ActionResult<ApiResponse<bool>>> ClearBasket()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<bool>.ErrorResult("User not authenticated"));
                }

                var result = await _basketService.ClearBasketAsync(userId.Value);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing basket for user");
                return StatusCode(500, ApiResponse<bool>.ErrorResult("Internal server error"));
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<object>>> GetBasketSummary()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<object>.ErrorResult("User not authenticated"));
                }

                var basketResult = await _basketService.GetBasketAsync(userId.Value);
                
                if (!basketResult.Success)
                {
                    return BadRequest(basketResult);
                }

                var basket = basketResult.Data;
                var summary = new
                {
                    TotalItems = basket?.TotalItems ?? 0,
                    TotalAmount = basket?.TotalAmount ?? 0m,
                    ItemCount = basket?.Items.Count ?? 0,
                    LastUpdated = basket?.UpdatedAt
                };

                return Ok(ApiResponse<object>.SuccessResult(summary));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving basket summary for user");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
            return null;
        }
    }
}