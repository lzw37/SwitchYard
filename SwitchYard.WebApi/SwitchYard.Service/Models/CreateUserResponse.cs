namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 创建用户响应模型
    /// </summary>
    public class CreateUserResponse
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
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
