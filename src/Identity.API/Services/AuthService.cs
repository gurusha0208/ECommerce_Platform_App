using Identity.API.Data;
using Identity.API.DTOs;
using Identity.API.Models;
using Common.Authentication;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Identity.API.Services
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto registerDto);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto loginDto);
        Task<ApiResponse<UserDto>> GetUserByIdAsync(int userId);
        Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email);
        Task<ApiResponse<bool>> UpdateUserAsync(int userId, UpdateUserDto updateUserDto);
        Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<ApiResponse<bool>> DeactivateUserAsync(int userId);
        Task<ApiResponse<bool>> ActivateUserAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly IdentityDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IdentityDbContext context, IJwtService jwtService, ILogger<AuthService> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == registerDto.Email.ToLower());

                if (existingUser != null)
                {
                    _logger.LogWarning("Registration attempt with existing email: {Email}", registerDto.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResult("User already exists with this email");
                }

                // Validate password strength
                var passwordValidation = ValidatePassword(registerDto.Password);
                if (!passwordValidation.IsValid)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResult("Password validation failed", passwordValidation.Errors);
                }

                // Hash password
                var passwordHash = HashPassword(registerDto.Password);

                // Create user
                var user = new User
                {
                    Email = registerDto.Email.ToLower(),
                    PasswordHash = passwordHash,
                    FirstName = registerDto.FirstName.Trim(),
                    LastName = registerDto.LastName.Trim(),
                    PhoneNumber = registerDto.PhoneNumber?.Trim() ?? string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Assign default role (Customer)
                var customerRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name == "Customer");

                if (customerRole != null)
                {
                    var userRole = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = customerRole.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserRoles.Add(userRole);
                    await _context.SaveChangesAsync();
                }

                // Load user with roles for token generation
                var userWithRoles = await GetUserWithRolesAsync(user.Id);
                var roles = userWithRoles?.UserRoles.Select(ur => ur.Role.Name).ToList() ?? new List<string>();

                // Generate JWT token
                var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, roles);

                var authResponse = new AuthResponseDto
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        Roles = roles,
                        CreatedAt = user.CreatedAt
                    },
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                _logger.LogInformation("User registered successfully: {UserId}, Email: {Email}", user.Id, user.Email);
                return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "User registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
                return ApiResponse<AuthResponseDto>.ErrorResult($"Error during registration: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto loginDto)
        {
            try
            {
                // Find user
                var user = await GetUserWithRolesAsync(loginDto.Email.ToLower());
                
                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", loginDto.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResult("Invalid email or password");
                }

                if (!VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid password attempt for user: {UserId}", user.Id);
                    return ApiResponse<AuthResponseDto>.ErrorResult("Invalid email or password");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
                    return ApiResponse<AuthResponseDto>.ErrorResult("Account is deactivated");
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Get roles
                var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

                // Generate JWT token
                var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, roles);

                var authResponse = new AuthResponseDto
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        Roles = roles,
                        CreatedAt = user.CreatedAt
                    },
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                _logger.LogInformation("User logged in successfully: {UserId}", user.Id);
                return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
                return ApiResponse<AuthResponseDto>.ErrorResult($"Error during login: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserDto>> GetUserByIdAsync(int userId)
        {
            try
            {
                var user = await GetUserWithRolesAsync(userId);
                
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return ApiResponse<UserDto>.ErrorResult("User not found");
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    CreatedAt = user.CreatedAt
                };

                return ApiResponse<UserDto>.SuccessResult(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user: {UserId}", userId);
                return ApiResponse<UserDto>.ErrorResult($"Error retrieving user: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email)
        {
            try
            {
                var user = await GetUserWithRolesAsync(email.ToLower());
                
                if (user == null)
                {
                    return ApiResponse<UserDto>.ErrorResult("User not found");
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    CreatedAt = user.CreatedAt
                };

                return ApiResponse<UserDto>.SuccessResult(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email: {Email}", email);
                return ApiResponse<UserDto>.ErrorResult($"Error retrieving user: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> UpdateUserAsync(int userId, UpdateUserDto updateUserDto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return ApiResponse<bool>.ErrorResult("User not found");
                }

                // Update fields if provided
                if (!string.IsNullOrWhiteSpace(updateUserDto.FirstName))
                    user.FirstName = updateUserDto.FirstName.Trim();

                if (!string.IsNullOrWhiteSpace(updateUserDto.LastName))
                    user.LastName = updateUserDto.LastName.Trim();

                if (!string.IsNullOrWhiteSpace(updateUserDto.PhoneNumber))
                    user.PhoneNumber = updateUserDto.PhoneNumber.Trim();

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User updated successfully: {UserId}", userId);
                return ApiResponse<bool>.SuccessResult(true, "User updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", userId);
                return ApiResponse<bool>.ErrorResult($"Error updating user: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return ApiResponse<bool>.ErrorResult("User not found");
                }

                // Verify current password
                if (!VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
                {
                    return ApiResponse<bool>.ErrorResult("Current password is incorrect");
                }

                // Validate new password
                var passwordValidation = ValidatePassword(changePasswordDto.NewPassword);
                if (!passwordValidation.IsValid)
                {
                    return ApiResponse<bool>.ErrorResult("New password validation failed", passwordValidation.Errors);
                }

                // Update password
                user.PasswordHash = HashPassword(changePasswordDto.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
                return ApiResponse<bool>.SuccessResult(true, "Password changed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return ApiResponse<bool>.ErrorResult($"Error changing password: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeactivateUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return ApiResponse<bool>.ErrorResult("User not found");
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User deactivated: {UserId}", userId);
                return ApiResponse<bool>.SuccessResult(true, "User deactivated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
                return ApiResponse<bool>.ErrorResult($"Error deactivating user: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> ActivateUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return ApiResponse<bool>.ErrorResult("User not found");
                }

                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User activated: {UserId}", userId);
                return ApiResponse<bool>.SuccessResult(true, "User activated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user: {UserId}", userId);
                return ApiResponse<bool>.ErrorResult($"Error activating user: {ex.Message}");
            }
        }

        private async Task<User?> GetUserWithRolesAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private async Task<User?> GetUserWithRolesAsync(string email)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var saltBytes = Encoding.UTF8.GetBytes("ECOMMERCE_SALT_2024"); // Use a proper salt in production
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var combined = new byte[passwordBytes.Length + saltBytes.Length];
            
            Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
            Buffer.BlockCopy(saltBytes, 0, combined, passwordBytes.Length, saltBytes.Length);
            
            var hashedBytes = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            var newHash = HashPassword(password);
            return newHash == hash;
        }

        private PasswordValidationResult ValidatePassword(string password)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(password))
            {
                errors.Add("Password is required");
                return new PasswordValidationResult { IsValid = false, Errors = errors };
            }

            if (password.Length < 8)
                errors.Add("Password must be at least 8 characters long");

            if (!password.Any(char.IsUpper))
                errors.Add("Password must contain at least one uppercase letter");

            if (!password.Any(char.IsLower))
                errors.Add("Password must contain at least one lowercase letter");

            if (!password.Any(char.IsDigit))
                errors.Add("Password must contain at least one number");

            if (!password.Any(c => "!@#$%^&*(),.?\":{}|<>".Contains(c)))
                errors.Add("Password must contain at least one special character");

            return new PasswordValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }
    }

    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // Additional DTOs needed
    public class UpdateUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}