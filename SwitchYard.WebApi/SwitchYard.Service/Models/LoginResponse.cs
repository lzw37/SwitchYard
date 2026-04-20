namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 登录响应模型
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// JWT Token
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Token类型
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// 过期时间（秒）
        /// </summary>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Refresh Token（长期令牌，用于续签 Access Token）
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Refresh Token 有效期（秒）
        /// </summary>
        public int RefreshTokenExpiresIn { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserID { get; set; } = string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Whether the current user must change password immediately.
        /// </summary>
        public bool MustChangePassword { get; set; }
    }
}
