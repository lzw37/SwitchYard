using System.Security.Cryptography;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// Refresh Token 服务：负责生成、验证、轮换和吊销 Refresh Token。
    /// Token 以明文存储于 SQLite 的 refreshtoken 表中，值本身是密码学安全随机数，
    /// 不具备"从用户秘密派生"的性质，无需单独哈希。
    /// </summary>
    public class RefreshTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly int _expirationDays;

        public RefreshTokenService(IConfiguration configuration, ILogger<RefreshTokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _expirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
        }

        /// <summary>
        /// 应用启动时调用，确保 refreshtoken 表存在。
        /// </summary>
        public void EnsureTableExists()
        {
            try
            {
                var db = DBConnector.GetDBConnector();
                db.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS refreshtoken (
                        token           TEXT PRIMARY KEY,
                        userid          TEXT NOT NULL,
                        expires         TEXT NOT NULL,
                        createdat       TEXT NOT NULL,
                        isrevoked       INTEGER NOT NULL DEFAULT 0,
                        replacedbyttoken TEXT
                    )");
                _logger.LogInformation("RefreshToken table ensured.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure refreshtoken table.");
                throw;
            }
        }

        /// <summary>
        /// 为指定用户创建并持久化一个新的 Refresh Token。
        /// </summary>
        public RefreshToken CreateToken(string userId)
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            // Base64Url 编码（无填充）
            var tokenString = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var now = DateTime.UtcNow;
            var token = new RefreshToken
            {
                Token = tokenString,
                UserId = userId,
                Expires = now.AddDays(_expirationDays),
                CreatedAt = now,
                IsRevoked = 0,
                ReplacedByToken = null
            };

            var db = DBConnector.GetDBConnector();
            db.ExecuteNonQuery(
                @"INSERT INTO refreshtoken (token, userid, expires, createdat, isrevoked, replacedbyttoken)
                  VALUES (@Token, @UserId, @Expires, @CreatedAt, @IsRevoked, @ReplacedByToken)",
                new
                {
                    Token = token.Token,
                    UserId = token.UserId,
                    Expires = token.Expires.ToString("o"),
                    CreatedAt = token.CreatedAt.ToString("o"),
                    IsRevoked = token.IsRevoked,
                    ReplacedByToken = token.ReplacedByToken
                });

            return token;
        }

        /// <summary>
        /// 查询单个 Refresh Token 记录（包含已吊销的）。
        /// </summary>
        public RefreshToken? GetToken(string tokenString)
        {
            var db = DBConnector.GetDBConnector();
            var rows = db.Query<RefreshTokenRow>(
                @"SELECT token, userid, expires, createdat, isrevoked, replacedbyttoken
                  FROM refreshtoken WHERE token = @Token",
                new { Token = tokenString });

            var row = rows?.FirstOrDefault();
            return row == null ? null : MapRow(row);
        }

        /// <summary>
        /// 轮换 Refresh Token：吊销旧 Token，生成并返回新 Token。
        /// 若旧 Token 无效（已吊销、已过期）则返回 null。
        /// </summary>
        public RefreshToken? Rotate(string oldTokenString)
        {
            var oldToken = GetToken(oldTokenString);

            if (oldToken == null)
            {
                _logger.LogWarning("Refresh token not found: {Token}", oldTokenString[..Math.Min(8, oldTokenString.Length)]);
                return null;
            }

            if (oldToken.IsRevoked == 1)
            {
                _logger.LogWarning("Attempted reuse of revoked refresh token for user {UserId}.", oldToken.UserId);
                // 安全考量：如果已吊销的 token 被再次使用，吊销该用户的全部 token（疑似盗用）
                RevokeAllTokensForUser(oldToken.UserId);
                return null;
            }

            if (oldToken.Expires <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token expired for user {UserId}.", oldToken.UserId);
                return null;
            }

            // 创建新 Token
            var newToken = CreateToken(oldToken.UserId);

            // 吊销旧 Token，记录其替代者
            var db = DBConnector.GetDBConnector();
            db.ExecuteNonQuery(
                @"UPDATE refreshtoken SET isrevoked = 1, replacedbyttoken = @NewToken WHERE token = @OldToken",
                new { NewToken = newToken.Token, OldToken = oldTokenString });

            return newToken;
        }

        /// <summary>
        /// 吊销指定 Refresh Token（用于登出）。
        /// </summary>
        public bool Revoke(string tokenString)
        {
            var db = DBConnector.GetDBConnector();
            var rows = db.ExecuteNonQuery(
                @"UPDATE refreshtoken SET isrevoked = 1 WHERE token = @Token AND isrevoked = 0",
                new { Token = tokenString });
            return rows > 0;
        }

        /// <summary>
        /// 吊销指定用户的所有有效 Refresh Token（检测到 token 盗用时调用）。
        /// </summary>
        public void RevokeAllTokensForUser(string userId)
        {
            var db = DBConnector.GetDBConnector();
            db.ExecuteNonQuery(
                @"UPDATE refreshtoken SET isrevoked = 1 WHERE userid = @UserId AND isrevoked = 0",
                new { UserId = userId });
            _logger.LogWarning("All refresh tokens revoked for user {UserId} due to suspected token reuse.", userId);
        }

        /// <summary>
        /// 清理过期且已吊销的历史 Token（可定期调用，避免表无限增长）。
        /// </summary>
        public void PurgeExpiredTokens()
        {
            var db = DBConnector.GetDBConnector();
            var cutoff = DateTime.UtcNow.AddDays(-1).ToString("o");
            db.ExecuteNonQuery(
                @"DELETE FROM refreshtoken WHERE expires < @Cutoff",
                new { Cutoff = cutoff });
        }

        /// <summary>
        /// Refresh Token 有效期（秒）。
        /// </summary>
        public int GetExpirationSeconds() => _expirationDays * 24 * 3600;

        // ----------------------------------------------------------------
        // 私有辅助
        // ----------------------------------------------------------------

        private static RefreshToken MapRow(RefreshTokenRow row) => new RefreshToken
        {
            Token = row.token,
            UserId = row.userid,
            Expires = DateTime.Parse(row.expires, null, System.Globalization.DateTimeStyles.RoundtripKind),
            CreatedAt = DateTime.Parse(row.createdat, null, System.Globalization.DateTimeStyles.RoundtripKind),
            IsRevoked = row.isrevoked,
            ReplacedByToken = row.replacedbyttoken
        };

        /// <summary>SQLite 行映射 DTO（全小写列名与 Dapper 默认映射对齐）。</summary>
        private class RefreshTokenRow
        {
            public string token { get; set; } = string.Empty;
            public string userid { get; set; } = string.Empty;
            public string expires { get; set; } = string.Empty;
            public string createdat { get; set; } = string.Empty;
            public int isrevoked { get; set; }
            public string? replacedbyttoken { get; set; }
        }
    }
}
