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
    }

    public class AuthService : IAuthService
    {
        private readonly IdentityDbContext _context;
        private readonly IJwtService _jwtService;

        public AuthService(IdentityDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == registerDto.Email);

                if (existingUser != null)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResult("User already exists with this email");
                }

                // Hash password
                var passwordHash = HashPassword(registerDto.Password);

                // Create user
                var user = new User
                {
                    Email = registerDto.Email,
                    PasswordHash = passwordHash,
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    PhoneNumber = registerDto.PhoneNumber
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
                        RoleId = customerRole.Id
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

                return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "User registered successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuthResponseDto>.ErrorResult($"Error during registration: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto loginDto)
        {
            try
            {
                // Find user
                var user = await GetUserWithRolesAsync(loginDto.Email);
                
                if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    return ApiResponse<AuthResponseDto>.ErrorResult("Invalid email or password");
                }

                if (!user.IsActive)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResult("Account is deactivated");
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
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

                return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Login successful");
            }
            catch (Exception ex)
            {
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
                return ApiResponse<UserDto>.ErrorResult($"Error retrieving user: {ex.Message}");
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
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "SALT_KEY"));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            var newHash = HashPassword(password);
            return newHash == hash;
        }
    }
}