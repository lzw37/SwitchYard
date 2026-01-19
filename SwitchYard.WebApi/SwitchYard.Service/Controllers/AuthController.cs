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
                    Name = user.Name,
                    UserID = user.Id,
                    Role = user.Role
                };

                _logger.LogInformation("用户登录成功: {Username}", user.Name);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登录过程中发生错误");
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
                    _logger.LogWarning("创建用户失败: 用户名已存在 - {Username}", request.Username);
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
                    _logger.LogError("创建用户失败 - {Username}", request.Username);
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

                _logger.LogInformation("用户创建成功: {Username}, ID: {UserId}", newUser.Name, newUser.Id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建用户过程中发生错误");
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
                    username = user.Name,
                    email = user.Email,
                    role = user.Role,
                    createdAt = user.CreateAt
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
