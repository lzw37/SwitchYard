using System.ComponentModel.DataAnnotations;

namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 创建用户请求模型
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度必须在3-50个字符之间")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "密码长度必须至少6个字符")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱
        /// </summary>
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        [StringLength(100, ErrorMessage = "邮箱长度不能超过100个字符")]
        public string? Email { get; set; }

        /// <summary>
        /// 角色（默认为User）
        /// </summary>
        [StringLength(50, ErrorMessage = "角色长度不能超过50个字符")]
        public string Role { get; set; } = "User";
    }
}
