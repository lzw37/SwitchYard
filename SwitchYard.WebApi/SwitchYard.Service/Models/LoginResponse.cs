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
        /// 用户名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = string.Empty;
    }
}
