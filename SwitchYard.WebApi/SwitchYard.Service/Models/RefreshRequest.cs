using System.ComponentModel.DataAnnotations;

namespace SwitchYard.Service.Models
{
    /// <summary>
    /// 刷新 Token 请求模型
    /// </summary>
    public class RefreshRequest
    {
        /// <summary>
        /// Refresh Token
        /// </summary>
        [Required(ErrorMessage = "RefreshToken 不能为空")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
