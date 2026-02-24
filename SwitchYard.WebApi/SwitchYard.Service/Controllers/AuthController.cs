using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Ocsp;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers
{
    /// <summary>
    /// 认证控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private string GetClientIpAddress()
        {
            // 优先检查 X-Forwarded-For，这是最常用的代理头部
            var forwardedFor = Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // X-Forwarded-For 可能包含多个IP（客户端,代理1,代理2...），取第一个
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

            // 检查 X-Real-IP，Nginx常用
            var realIp = Request?.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp) && IsValidIpAddress(realIp))
            {
                return realIp;
            }

            // 检查 X-Forwarded-For 的其他变体
            var cfConnectingIp = Request?.Headers["CF-Connecting-IP"].FirstOrDefault(); // Cloudflare
            if (!string.IsNullOrWhiteSpace(cfConnectingIp) && IsValidIpAddress(cfConnectingIp))
            {
                return cfConnectingIp;
            }

            var xForwardedFor = Request?.Headers["X_FORWARDED_FOR"].FirstOrDefault(); // 下划线变体
            if (!string.IsNullOrWhiteSpace(xForwardedFor) && IsValidIpAddress(xForwardedFor))
            {
                return xForwardedFor;
            }

            // 检查 True-Client-IP
            var trueClientIp = Request?.Headers["True-Client-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(trueClientIp) && IsValidIpAddress(trueClientIp))
            {
                return trueClientIp;
            }

            // 最后回退到连接的远程IP地址
            var remoteIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                // 处理IPv6映射的IPv4地址
                if (remoteIp.StartsWith("::ffff:"))
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

        private bool IsValidIpAddress(string ipString)
        {
            // 验证IP地址格式并排除本地/私有地址（在生产环境中可能需要这些地址）
            if (System.Net.IPAddress.TryParse(ipString, out var ipAddress))
            {
                // 排除明显的本地地址
                if (ipString == "127.0.0.1" || ipString == "::1" || ipString == "localhost")
                {
                    return false;
                }
                return true;
            }
            return false;
        }

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

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求</param>
        /// <returns>登录结果</returns>
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                // 验证用户
                var user = _userService.ValidateUser(request.Username, request.Password);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: Invalid username or password - {Username}, ClientIp: {ClientIp}", request.Username, GetClientIpAddress());
                    return Unauthorized(new { message = "用户名或密码错误" });
                }

                // 生成Token
                var token = _jwtTokenService.GenerateToken(user);

                var response = new LoginResponse
                {
                    Token = token,
                    TokenType = "Bearer",
                    ExpiresIn = _jwtTokenService.GetExpirationSeconds(),
                    Name = user.Name,
                    UserID = user.Id,
                    Role = user.Role
                };

                _logger.LogInformation("User logged in successfully: {Username}, ClientIp: {ClientIp}", user.Name, GetClientIpAddress());
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login: {ErrorMessage}, ClientIp: {ClientIp}", ex.Message, GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="request">创建用户请求</param>
        /// <returns>创建结果</returns>
        [HttpPost("createuser")]
        public IActionResult CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // 验证请求
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "请求参数无效", errors = ModelState });
                }

                // 检查用户名是否已存在
                var existingUser = _userService.GetUserByUsername(request.Username);
                if (existingUser != null)
                {
                    _logger.LogWarning("User creation failed: Username already exists - {Username}, ClientIp: {ClientIp}", request.Username, GetClientIpAddress());
                    return Conflict(new { message = "用户名已存在" });
                }

                // 创建用户（直接使用传入的密码作为哈希值）
                var newUser = _userService.CreateUser(
                    request.Username,
                    request.Password,
                    request.Email,
                    request.Role
                );

                if (newUser == null)
                {
                    _logger.LogError("User creation failed - {Username}, ClientIp: {ClientIp}", request.Username, GetClientIpAddress());
                    return StatusCode(500, new { message = "创建用户失败" });
                }

                var response = new CreateUserResponse
                {
                    Id = newUser.Id,
                    Name = newUser.Name,
                    Email = newUser.Email,
                    Role = newUser.Role,
                    CreatedAt = newUser.CreateAt,
                    Message = "用户创建成功"
                };

                _logger.LogInformation("User created successfully: {Username}, ID: {UserId}, ClientIp: {ClientIp}", newUser.Name, newUser.Id, GetClientIpAddress());
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the user, ClientIp: {ClientIp}", GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 验证Token
        /// </summary>
        /// <returns>验证结果</returns>
        [HttpGet("validate")]
        public IActionResult ValidateToken()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Token缺失或格式错误" });
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var principal = _jwtTokenService.ValidateToken(token);

                if (principal == null)
                {
                    return Unauthorized(new { message = "Token无效或已过期" });
                }

                var username = principal.Identity?.Name;
                var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                return Ok(new
                {
                    message = "Token有效",
                    username = username,
                    role = role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while validating the token, ClientIp: {ClientIp}", GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns>用户信息</returns>
        [HttpGet("userinfo")]
        public IActionResult GetUserInfo()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { message = "未授权" });
                }

                var user = _userService.GetUserByUsername(username);
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
                    createdAt = user.CreateAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving user information, ClientIp: {ClientIp}", GetClientIpAddress());
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }
    }
}
