namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 管理员更新用户请求
    /// </summary>
    public class AdminUpdateUserRequest
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
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// 激活状态（1=激活，0=未激活）
        /// </summary>
        public uint IsActive { get; set; } = 1;
    }
}
