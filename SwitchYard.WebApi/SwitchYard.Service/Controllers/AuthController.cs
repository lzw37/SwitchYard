using Microsoft.AspNetCore.Mvc;
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
                    _logger.LogWarning("登录失败: 用户名或密码错误 - {Username}", request.Username);
                    return Unauthorized(new { message = "用户名或密码错误" });
                }

                // 生成Token
                var token = _jwtTokenService.GenerateToken(user);

                var response = new LoginResponse
                {
                    Token = token,
                    TokenType = "Bearer",
                    ExpiresIn = _jwtTokenService.GetExpirationSeconds(),
                    Username = user.Username,
                    Role = user.Role
                };

                _logger.LogInformation("用户登录成功: {Username}", user.Username);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登录过程中发生错误");
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
                _logger.LogError(ex, "Token验证过程中发生错误");
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
                    username = user.Username,
                    email = user.Email,
                    role = user.Role,
                    createdAt = user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户信息过程中发生错误");
                return StatusCode(500, new { message = "服务器内部错误" });
            }
        }
    }
}
