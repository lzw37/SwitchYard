using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(UserService userService, ILogger<AdminController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            try
            {
                var users = _userService.GetAllUsers()
                    .Select(user => new
                    {
                        id = user.Id,
                        name = user.Name,
                        role = user.Role,
                        email = user.Email,
                        createAt = user.CreateAt,
                        isActive = user.IsActive
                    });

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user list");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] AdminCreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Username and password cannot be empty" });
                }

                if (request.IsActive != 0 && request.IsActive != 1)
                {
                    return BadRequest(new { message = "isActive must be 0 or 1" });
                }

                var normalizedRole = NormalizeRole(request.Role);
                if (normalizedRole == null)
                {
                    return BadRequest(new { message = "Invalid role. Allowed values are User or Admin." });
                }

                var username = request.Username.Trim();
                var existingUser = _userService.GetUserByUsername(username);
                if (existingUser != null)
                {
                    return Conflict(new { message = "Username already exists" });
                }

                var createdUser = _userService.CreateUser(
                    username,
                    request.Password.Trim(),
                    string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    normalizedRole,
                    request.IsActive
                );

                if (createdUser == null)
                {
                    return StatusCode(500, new { message = "Failed to create user" });
                }

                return Ok(new
                {
                    id = createdUser.Id,
                    name = createdUser.Name,
                    role = createdUser.Role,
                    email = createdUser.Email,
                    createAt = createdUser.CreateAt,
                    isActive = createdUser.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("users/{id}")]
        public IActionResult UpdateUser([FromRoute] string id, [FromBody] AdminUpdateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                var currentUser = _userService.GetUserById(id);
                if (currentUser == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (string.IsNullOrWhiteSpace(request.Id) ||
                    string.IsNullOrWhiteSpace(request.Name) ||
                    string.IsNullOrWhiteSpace(request.Role))
                {
                    return BadRequest(new { message = "Request parameters are invalid. User ID, name, and role cannot be empty." });
                }

                if (request.IsActive != 0 && request.IsActive != 1)
                {
                    return BadRequest(new { message = "isActive must be 0 or 1" });
                }

                var normalizedRole = NormalizeRole(request.Role);
                if (normalizedRole == null)
                {
                    return BadRequest(new { message = "Invalid role. Allowed values are User or Admin." });
                }

                var newUserId = request.Id.Trim();
                var newUsername = request.Name.Trim();

                if (!string.Equals(currentUser.Id, newUserId, StringComparison.Ordinal))
                {
                    var userById = _userService.GetUserById(newUserId);
                    if (userById != null)
                    {
                        return Conflict(new { message = "User ID already exists" });
                    }
                }

                if (!string.Equals(currentUser.Name, newUsername, StringComparison.OrdinalIgnoreCase))
                {
                    var userByName = _userService.GetUserByUsername(newUsername);
                    if (userByName != null)
                    {
                        return Conflict(new { message = "Username already exists" });
                    }
                }

                var updatedUser = new User
                {
                    Id = newUserId,
                    Name = newUsername,
                    PasswordHash = currentUser.PasswordHash,
                    Role = normalizedRole,
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    CreateAt = request.CreateAt == default ? currentUser.CreateAt : request.CreateAt,
                    IsActive = request.IsActive
                };

                var updated = _userService.UpdateUser(currentUser.Id, updatedUser);
                if (!updated)
                {
                    return StatusCode(500, new { message = "Failed to update user" });
                }

                return Ok(new
                {
                    id = updatedUser.Id,
                    name = updatedUser.Name,
                    role = updatedUser.Role,
                    email = updatedUser.Email,
                    createAt = updatedUser.CreateAt,
                    isActive = updatedUser.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("users/{id}/reset-password")]
        public IActionResult ResetUserPassword([FromRoute] string id, [FromBody] AdminResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                if (string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new { message = "New password cannot be empty" });
                }

                var user = _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var updated = _userService.ResetUserPassword(user.Id, request.NewPassword.Trim());
                if (!updated)
                {
                    return StatusCode(500, new { message = "Failed to reset password" });
                }

                return Ok(new { message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset password: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser([FromRoute] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                var user = _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var currentUsername = User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(currentUsername) &&
                    string.Equals(currentUsername, user.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "You cannot delete the currently logged-in account" });
                }

                if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var adminCount = _userService
                        .GetAllUsers()
                        .Count(u => string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase));

                    if (adminCount <= 1)
                    {
                        return BadRequest(new { message = "Cannot delete the last admin account" });
                    }
                }

                var deleted = _userService.DeleteUser(user.Id);
                if (!deleted)
                {
                    return StatusCode(500, new { message = "Failed to delete user" });
                }

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        private static string? NormalizeRole(string role)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "User";
            }

            return null;
        }
    }
}

