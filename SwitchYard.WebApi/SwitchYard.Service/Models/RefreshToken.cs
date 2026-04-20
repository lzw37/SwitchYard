namespace SwitchYard.Service.Models
{
    /// <summary>
    /// Refresh Token 模型（对应数据库 refreshtoken 表）
    /// </summary>
    public class RefreshToken
    {
        /// <summary>Token 值（Base64Url 编码的 256-bit 随机数）</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>所属用户 ID</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>过期时间（UTC）</summary>
        public DateTime Expires { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>是否已吊销（0=有效，1=已吊销）</summary>
        public int IsRevoked { get; set; }

        /// <summary>轮换后替代本 Token 的新 Token（可为 null）</summary>
        public string? ReplacedByToken { get; set; }
    }
}
