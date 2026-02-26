namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 管理员重置密码请求
    /// </summary>
    public class AdminResetPasswordRequest
    {
        /// <summary>
        /// 新密码（前端SHA-256后字符串）
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;
    }
}
