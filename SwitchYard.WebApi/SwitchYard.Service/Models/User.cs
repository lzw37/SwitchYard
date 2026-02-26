namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 用户模型
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = "User";

        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 是否激活
        /// </summary>
        public uint IsActive { get; set; } = 1;

        /// <summary>
        /// Whether the user must change password on next login.
        /// 1 = required, 0 = not required.
        /// </summary>
        public uint MustChangePassword { get; set; } = 0;
    }
}
