using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly RefreshTokenService _refreshTokenService;
        private readonly UserService _userService;
        private readonly HumpInstanceCopyService _humpInstanceCopyService;
        private readonly ILogger<AuthController> _logger;
        private const string DefaultTemplateInstanceId = "001";

        public AuthController(
            JwtTokenService jwtTokenService,
            RefreshTokenService refreshTokenService,
            UserService userService,
            HumpInstanceCopyService humpInstanceCopyService,
            ILogger<AuthController> logger)
        {
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _userService = userService;
            _humpInstanceCopyService = humpInstanceCopyService;
            _logger = logger;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = _userService.ValidateUser(request.Username, request.Password);
                if (user == null)
                {
                    // To prevent username enumeration we return a single generic response
                    // for all authentication failures (wrong credentials, inactive account, etc.).
                    // The specific reason is only recorded in server-side logs.
                    var registeredUser = _userService.GetUserByUsername(request.Username);
                    if (registeredUser != null &&
                        registeredUser.IsActive != 1 &&
                        UserService.VerifyPassword(request.Password, registeredUser.PasswordHash))
                    {
                        _logger.LogWarning(
                            "Login failed: inactive user - {Username}, ClientIp: {ClientIp}",
                            request.Username,
                            GetClientIpAddress());
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Login failed: invalid username or password - {Username}, ClientIp: {ClientIp}",
                            request.Username,
                            GetClientIpAddress());
                    }

                    return Unauthorized(new { message = "用户名或密码错误，或账号尚未激活" });
                }

                var token = _jwtTokenService.GenerateToken(user);
                var refreshToken = _refreshTokenService.CreateToken(user.Id);

                var response = new LoginResponse
                {
                    Token = token,
                    TokenType = "Bearer",
                    ExpiresIn = _jwtTokenService.GetExpirationSeconds(),
                    RefreshToken = refreshToken.Token,
                    RefreshTokenExpiresIn = _refreshTokenService.GetExpirationSeconds(),
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

        /// <summary>
        /// 使用有效的 Refresh Token 换取新的 Access Token 和 Refresh Token（令牌轮换）。
        /// </summary>
        [HttpPost("refresh")]
        [EnableRateLimiting("auth")]
        public IActionResult Refresh([FromBody] RefreshRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "请求参数无效" });
                }

                // 轮换 Refresh Token（内部已校验是否吊销/过期，并处理疑似盗用情形）
                var newRefreshToken = _refreshTokenService.Rotate(request.RefreshToken);
                if (newRefreshToken == null)
                {
                    return Unauthorized(new { message = "Refresh Token 无效或已过期" });
                }

                var user = _userService.GetActiveUserById(newRefreshToken.UserId);
                if (user == null)
                {
                    // 用户已被禁用，吊销刚创建的 token
                    _refreshTokenService.Revoke(newRefreshToken.Token);
                    return Unauthorized(new { message = "用户不存在或已被禁用" });
                }

                var accessToken = _jwtTokenService.GenerateToken(user);

                _logger.LogInformation(
                    "Token refreshed for user {Username}, ClientIp: {ClientIp}",
                    user.Name,
                    GetClientIpAddress());

                return Ok(new
                {
                    token = accessToken,
                    tokenType = "Bearer",
                    expiresIn = _jwtTokenService.GetExpirationSeconds(),
                    refreshToken = newRefreshToken.Token,
                    refreshTokenExpiresIn = _refreshTokenService.GetExpirationSeconds()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred during token refresh, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 登出：吊销指定 Refresh Token。
        /// </summary>
        [HttpPost("logout")]
        public IActionResult Logout([FromBody] RefreshRequest request)
        {
            try
            {
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return BadRequest(new { message = "请求参数无效" });
                }

                _refreshTokenService.Revoke(request.RefreshToken);

                _logger.LogInformation(
                    "User logged out, ClientIp: {ClientIp}",
                    GetClientIpAddress());

                return Ok(new { message = "已成功登出" });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred during logout, ClientIp: {ClientIp}",
                    GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        [HttpPost("createuser")]
        [EnableRateLimiting("register")]
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

                var defaultInstanceCopyResult = _humpInstanceCopyService.CopyInstance(
                    DefaultTemplateInstanceId,
                    string.Empty,
                    newUser.Name);
                if (!defaultInstanceCopyResult.Success)
                {
                    _logger.LogError(
                        "Default hump instance initialization failed for user {Username}, UserId: {UserId}, TemplateInstanceId: {TemplateInstanceId}, StatusCode: {StatusCode}, Error: {ErrorMessage}, ClientIp: {ClientIp}",
                        newUser.Name,
                        newUser.Id,
                        DefaultTemplateInstanceId,
                        defaultInstanceCopyResult.StatusCode,
                        defaultInstanceCopyResult.ErrorMessage,
                        GetClientIpAddress());

                    var rollbackSucceeded = _userService.DeleteUser(newUser.Id);
                    if (!rollbackSucceeded)
                    {
                        _logger.LogError(
                            "User rollback failed after default hump instance initialization error for user {Username}, UserId: {UserId}, ClientIp: {ClientIp}",
                            newUser.Name,
                            newUser.Id,
                            GetClientIpAddress());
                        return StatusCode(500, new { message = "鍒涘缓榛樿瀹炰緥澶辫触锛岃鑱旂郴绠＄悊鍛?" });
                    }

                    return StatusCode(500, new { message = "鍒涘缓榛樿瀹炰緥澶辫触锛岃绋嶅悗閲嶈瘯" });
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
        [EnableRateLimiting("auth")]
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
            // Only trust HttpContext.Connection.RemoteIpAddress. The ASP.NET Core
            // ForwardedHeaders middleware (configured in Program.cs with an explicit
            // KnownProxies / KnownNetworks list) is responsible for safely replacing
            // this value from X-Forwarded-For when requests come from trusted proxies.
            // User-controllable headers MUST NOT be read directly here because they
            // can be spoofed, allowing audit / rate-limit bypass.
            var remoteIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
            {
                return "unknown";
            }

            if (remoteIp.StartsWith("::ffff:", StringComparison.Ordinal))
            {
                remoteIp = remoteIp.Substring(7);
            }

            return remoteIp;
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
