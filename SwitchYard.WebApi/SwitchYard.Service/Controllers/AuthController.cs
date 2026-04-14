using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using System.Security.Claims;

namespace SwitchYard.Service.Controllers
{
    /// <summary>
    /// Authentication related APIs.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly UserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            JwtTokenService jwtTokenService,
            UserService userService,
            ILogger<AuthController> logger)
        {
            _jwtTokenService = jwtTokenService;
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = _userService.ValidateUser(request.Username, request.Password);
                if (user == null)
                {
                    var registeredUser = _userService.GetUserByUsername(request.Username);
                    if (registeredUser != null &&
                        registeredUser.IsActive != 1 &&
                        UserService.VerifyPassword(request.Password, registeredUser.PasswordHash))
                    {
                        _logger.LogWarning(
                            "Login failed: inactive user - {Username}, ClientIp: {ClientIp}",
                            request.Username,
                            GetClientIpAddress());
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "账号未激活，请联系管理员在用户管理中激活" });
                    }

                    _logger.LogWarning(
                        "Login failed: invalid username or password - {Username}, ClientIp: {ClientIp}",
                        request.Username,
                        GetClientIpAddress());
                    return Unauthorized(new { message = "用户名或密码错误" });
                }

                var token = _jwtTokenService.GenerateToken(user);

                var response = new LoginResponse
                {
                    Token = token,
                    TokenType = "Bearer",
                    ExpiresIn = _jwtTokenService.GetExpirationSeconds(),
                    Name = user.Name,
                    UserID = user.Id,
                    Role = user.Role,
                    MustChangePassword = user.MustChangePassword == 1
                };

                _logger.LogInformation(
                    "User logged in successfully: {Username}, ClientIp: {ClientIp}",
                    user.Name,
                    GetClientIpAddress());

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred during login: {ErrorMessage}, ClientIp: {ClientIp}",
                    ex.Message,
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        [HttpPost("createuser")]
        public IActionResult CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "请求参数无效", errors = ModelState });
                }

                var username = request.Username.Trim();
                var normalizedRole = NormalizeRole(request.Role);
                if (normalizedRole == null)
                {
                    return BadRequest(new { message = "Invalid role. Allowed values are User or Admin." });
                }

                var existingUser = _userService.GetUserByUsername(username);
                if (existingUser != null)
                {
                    _logger.LogWarning(
                        "User creation failed: username already exists - {Username}, ClientIp: {ClientIp}",
                        request.Username,
                        GetClientIpAddress());
                    return Conflict(new { message = "用户名已存在" });
                }

                var isAdminRegistration = string.Equals(normalizedRole, "Admin", StringComparison.Ordinal);
                var isActive = isAdminRegistration ? 0u : 1u;
                var newUser = _userService.CreateUser(
                    username,
                    request.Password.Trim(),
                    string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    normalizedRole,
                    isActive);

                if (newUser == null)
                {
                    _logger.LogError(
                        "User creation failed - {Username}, ClientIp: {ClientIp}",
                        request.Username,
                        GetClientIpAddress());
                    return StatusCode(500, new { message = "创建用户失败" });
                }

                var response = new CreateUserResponse
                {
                    Id = newUser.Id,
                    Name = newUser.Name,
                    Email = newUser.Email,
                    Role = newUser.Role,
                    CreatedAt = newUser.CreateAt,
                    IsActive = newUser.IsActive,
                    Message = "用户创建成功"
                };

                _logger.LogInformation(
                    "User created successfully: {Username}, ID: {UserId}, ClientIp: {ClientIp}",
                    newUser.Name,
                    newUser.Id,
                    GetClientIpAddress());

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while creating the user, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        [Authorize]
        [HttpGet("userinfo")]
        public IActionResult GetUserInfo()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "未授权" });
                }

                var user = _userService.GetActiveUserById(userId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                return Ok(new
                {
                    id = user.Id,
                    username = user.Name,
                    email = user.Email,
                    role = user.Role,
                    createdAt = user.CreateAt,
                    mustChangePassword = user.MustChangePassword == 1
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while retrieving user information, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        [Authorize]
        [HttpPut("userinfo")]
        public IActionResult UpdateUserInfo([FromBody] UpdateUserInfoRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "请求参数无效", errors = ModelState });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "未授权" });
                }

                var user = _userService.GetActiveUserById(userId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                    ? null
                    : request.Email.Trim();

                var updated = _userService.UpdateUserEmail(userId, normalizedEmail);
                if (!updated)
                {
                    return StatusCode(500, new { message = "更新用户信息失败" });
                }

                return Ok(new { message = "用户信息更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while updating user information, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        [Authorize]
        [HttpPost("changepassword")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new { message = "旧密码和新密码不能为空" });
                }

                if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "新密码不能和旧密码相同" });
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { message = "未授权" });
                }

                var user = _userService.GetActiveUserById(userId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                if (!UserService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                {
                    return BadRequest(new { message = "旧密码错误" });
                }

                var updated = _userService.ResetUserPassword(user.Id, request.NewPassword);
                if (!updated)
                {
                    return StatusCode(500, new { message = "修改密码失败" });
                }

                _logger.LogInformation(
                    "User changed password successfully: {Username}, ClientIp: {ClientIp}",
                    user.Name,
                    GetClientIpAddress());

                return Ok(new { message = "密码修改成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while changing password, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private string GetClientIpAddress()
        {
            var forwardedFor = Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var ip in ips)
                {
                    var trimmedIp = ip.Trim();
                    if (IsValidIpAddress(trimmedIp))
                    {
                        return trimmedIp;
                    }
                }
            }

            var realIp = Request?.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp) && IsValidIpAddress(realIp))
            {
                return realIp;
            }

            var cfConnectingIp = Request?.Headers["CF-Connecting-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cfConnectingIp) && IsValidIpAddress(cfConnectingIp))
            {
                return cfConnectingIp;
            }

            var xForwardedFor = Request?.Headers["X_FORWARDED_FOR"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xForwardedFor) && IsValidIpAddress(xForwardedFor))
            {
                return xForwardedFor;
            }

            var trueClientIp = Request?.Headers["True-Client-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(trueClientIp) && IsValidIpAddress(trueClientIp))
            {
                return trueClientIp;
            }

            var remoteIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                if (remoteIp.StartsWith("::ffff:", StringComparison.Ordinal))
                {
                    remoteIp = remoteIp.Substring(7);
                }

                if (IsValidIpAddress(remoteIp))
                {
                    return remoteIp;
                }
            }

            return "unknown";
        }

        private static bool IsValidIpAddress(string ipString)
        {
            if (!System.Net.IPAddress.TryParse(ipString, out _))
            {
                return false;
            }

            if (ipString == "127.0.0.1" || ipString == "::1" || ipString == "localhost")
            {
                return false;
            }

            return true;
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
