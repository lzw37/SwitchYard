using SwitchYard.Service.Models;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// 用户服务
    /// </summary>
    public class UserService
    {
        // 模拟用户数据库（实际应用中应该使用数据库）
        private static readonly List<User> _users = new List<User>
        {
            new User
            {
                Id = 1,
                Username = "admin",
                Password = HashPassword("240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9"), // 密码: admin123
                Role = "Admin",
                Email = "admin@switchyard.com",
                IsActive = true
            },
            new User
            {
                Id = 2,
                Username = "user",
                Password = HashPassword("user123"), // 密码: user123
                Role = "User",
                Email = "user@switchyard.com",
                IsActive = true
            }
        };

        /// <summary>
        /// 验证用户登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>用户信息，如果验证失败则返回null</returns>
        public User? ValidateUser(string username, string password)
        {
            var user = _users.FirstOrDefault(u =>
                u.Username == username &&
                u.IsActive);

            if (user == null)
                return null;

            // 使用Argon2id验证密码
            if (VerifyPassword(password, user.Password))
                return user;

            return null;
        }

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户信息</returns>
        public User? GetUserByUsername(string username)
        {
            return _users.FirstOrDefault(u => u.Username == username && u.IsActive);
        }

        /// <summary>
        /// 根据ID获取用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户信息</returns>
        public User? GetUserById(int id)
        {
            return _users.FirstOrDefault(u => u.Id == id && u.IsActive);
        }

        /// <summary>
        /// 密码哈希（使用Argon2id）
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>哈希后的密码（包含盐值）</returns>
        private static string HashPassword(string password)
        {
            // 生成16字节的随机盐值
            var salt = RandomNumberGenerator.GetBytes(16);

            // 使用Argon2id进行密码哈希
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 8,      // 并行度
                MemorySize = 65536,  // 内存使用量（64 MB）
                Iterations = 4  // 迭代次数
            };

            // 生成32字节的哈希值
            var hash = argon2.GetBytes(32);

            // 将盐值和哈希值组合存储：盐值(16字节) + 哈希值(32字节)
            var hashWithSalt = new byte[48];
            Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
            Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);

            // 返回Base64编码的字符串
            return Convert.ToBase64String(hashWithSalt);
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="hashedPassword">哈希后的密码（包含盐值）</param>
        /// <returns>密码是否匹配</returns>
        private static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                // 解码存储的哈希值
                var hashWithSalt = Convert.FromBase64String(hashedPassword);

                // 提取盐值（前16字节）
                var salt = new byte[16];
                Buffer.BlockCopy(hashWithSalt, 0, salt, 0, 16);

                // 提取哈希值（后32字节）
                var storedHash = new byte[32];
                Buffer.BlockCopy(hashWithSalt, 16, storedHash, 0, 32);

                // 使用相同的参数和盐值重新计算哈希
                using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
                {
                    Salt = salt,
                    DegreeOfParallelism = 8,
                    MemorySize = 65536,
                    Iterations = 4
                };

                var computedHash = argon2.GetBytes(32);

                // 使用恒定时间比较防止时间攻击
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
