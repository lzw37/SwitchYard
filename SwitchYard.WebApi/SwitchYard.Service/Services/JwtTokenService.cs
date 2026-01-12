using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// JWT Token服务
    /// </summary>
    public class JwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expirationMinutes;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _secretKey = _configuration["Jwt:SecretKey"] ?? throw new ArgumentNullException("Jwt:SecretKey 配置项缺失");
            _issuer = _configuration["Jwt:Issuer"] ?? "SwitchYard.Service";
            _audience = _configuration["Jwt:Audience"] ?? "SwitchYard.Client";
            _expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        }

        /// <summary>
        /// 生成JWT Token
        /// </summary>
        /// <param name="user">用户信息</param>
        /// <returns>JWT Token字符串</returns>
        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// 验证Token并获取用户信息
        /// </summary>
        /// <param name="token">JWT Token</param>
        /// <returns>ClaimsPrincipal</returns>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从Token中获取用户ID
        /// </summary>
        /// <param name="token">JWT Token</param>
        /// <returns>用户ID</returns>
        public string? GetUserIdFromToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null) return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }

        /// <summary>
        /// 获取Token过期时间（秒）
        /// </summary>
        /// <returns>过期时间（秒）</returns>
        public int GetExpirationSeconds()
        {
            return _expirationMinutes * 60;
        }
    }
}
