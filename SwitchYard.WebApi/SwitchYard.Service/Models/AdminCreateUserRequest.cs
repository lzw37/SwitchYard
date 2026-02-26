namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 管理员新增用户请求
    /// </summary>
    public class AdminCreateUserRequest
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码（前端SHA-256后字符串）
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 角色（User 或 Admin）
        /// </summary>
        public string Role { get; set; } = "User";

        /// <summary>
        /// 激活状态（1=激活，0=未激活）
        /// </summary>
        public uint IsActive { get; set; } = 1;
    }
}
