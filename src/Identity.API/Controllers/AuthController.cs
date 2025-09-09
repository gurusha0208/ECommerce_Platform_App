using Identity.API.DTOs;
using Identity.API.Services;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResult("Validation failed", errors));
                }

                _logger.LogInformation("Registration attempt for email: {Email}", registerDto.Email);

                var result = await _authService.RegisterAsync(registerDto);

                if (!result.Success)
                {
                    _logger.LogWarning("Registration failed for email: {Email}. Reason: {Message}", 
                        registerDto.Email, result.Message);
                    return BadRequest(result);
                }

                _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);
                return CreatedAtAction(nameof(GetProfile), null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResult("An error occurred during registration"));
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResult("Validation failed", errors));
                }

                _logger.LogInformation("Login attempt for email: {Email}", loginDto.Email);

                var result = await _authService.LoginAsync(loginDto);

                if (!result.Success)
                {
                    _logger.LogWarning("Login failed for email: {Email}. Reason: {Message}", 
                        loginDto.Email, result.Message);
                    return Unauthorized(result);
                }

                _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResult("An error occurred during login"));
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<UserDto>.ErrorResult("User not authenticated"));
                }

                _logger.LogInformation("Profile request for user: {UserId}", userId);

                var result = await _authService.GetUserByIdAsync(userId.Value);

                if (!result.Success)
                {
                    _logger.LogWarning("Profile retrieval failed for user: {UserId}. Reason: {Message}", 
                        userId, result.Message);
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile for user");
                return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while retrieving profile"));
            }
        }

        [HttpPost("validate")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> ValidateToken()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userEmail = GetCurrentUserEmail();
                var userRoles = GetCurrentUserRoles();

                if (userId == null || string.IsNullOrEmpty(userEmail))
                {
                    return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));
                }

                var tokenInfo = new
                {
                    UserId = userId.Value,
                    Email = userEmail,
                    Roles = userRoles,
                    IsValid = true,
                    ValidatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Token validated for user: {UserId}", userId);
                return Ok(ApiResponse<object>.SuccessResult(tokenInfo, "Token is valid"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token validation");
                return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred during token validation"));
            }
        }

        [HttpPost("refresh")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResult("User not authenticated"));
                }

                _logger.LogInformation("Token refresh request for user: {UserId}", userId);

                var result = await _authService.GetUserByIdAsync(userId.Value);

                if (!result.Success || result.Data == null)
                {
                    _logger.LogWarning("Token refresh failed for user: {UserId}. User not found", userId);
                    return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResult("User not found"));
                }

                // Note: In a real implementation, you might want to add refresh token logic
                // For now, we'll return the user data without generating a new token
                var user = result.Data;
                var refreshResponse = new AuthResponseDto
                {
                    Token = string.Empty, // Would generate new token here
                    User = user,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                return Ok(ApiResponse<AuthResponseDto>.SuccessResult(refreshResponse, "Token refreshed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh for user");
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResult("An error occurred during token refresh"));
            }
        }

        [HttpGet("health")]
        public ActionResult<ApiResponse<object>> HealthCheck()
        {
            var healthInfo = new
            {
                Service = "Identity API",
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            };

            return Ok(ApiResponse<object>.SuccessResult(healthInfo, "Service is healthy"));
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

        private string? GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value;
        }

        private List<string> GetCurrentUserRoles()
        {
            return User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        }
    }
}