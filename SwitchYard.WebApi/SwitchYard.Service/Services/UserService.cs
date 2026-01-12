using SwitchYard.Service.Models;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// 用户服务
    /// </summary>
    public class UserService
    {
        private readonly ILogger<UserService> _logger;

        // 雪花算法ID生成器（静态单例，workerId=1, datacenterId=1）
        private static readonly SnowflakeIdGenerator _idGenerator = new SnowflakeIdGenerator(workerId: 1, datacenterId: 1);

        public UserService(ILogger<UserService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 验证用户登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>用户信息，如果验证失败则返回null</returns>
        public User? ValidateUser(string username, string password)
        {
            try
            {
                var user = GetUserByUsername(username);
                if (user == null || user.IsActive != 1)
                {
                    return null;
                }

                // 直接比较密码哈希值
                if (user.PasswordHash == password)
                {
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证用户时发生错误: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户信息</returns>
        public User? GetUserByUsername(string username)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE name = @Username AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Username = username });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据用户名获取用户时发生错误: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 根据ID获取用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户信息</returns>
        public User? GetUserById(string id)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE id = @Id AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Id = id });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据ID获取用户时发生错误: {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（将被加密存储）</param>
        /// <param name="email">邮箱</param>
        /// <param name="role">角色</param>
        /// <returns>创建的用户信息，失败返回null</returns>
        public User? CreateUser(string username, string password, string? email = null, string role = "User")
        {
            try
            {
                // 检查用户名是否已存在
                var existingUser = GetUserByUsername(username);
                if (existingUser != null)
                {
                    _logger.LogWarning("创建用户失败: 用户名已存在 - {Username}", username);
                    return null;
                }

                // 使用雪花算法生成新的用户ID
                var userId = _idGenerator.NextIdString();

                // 创建用户对象
                var newUser = new User
                {
                    Id = userId,
                    Name = username,
                    PasswordHash = password, // 直接存储密码哈希（调用方应传入哈希值）
                    Email = email,
                    Role = role,
                    CreateAt = DateTime.Now,
                    IsActive = 1
                };

                // 插入数据库
                var dbConnector = DBConnector.GetDBConnector();
                var sql = @"INSERT INTO user (id, name, passwordhash, role, email, createat, isactive) 
                            VALUES (@Id, @Name, @PasswordHash, @Role, @Email, @CreateAt, @IsActive)";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = newUser.Id,
                    Name = newUser.Name,
                    PasswordHash = newUser.PasswordHash,
                    Role = newUser.Role,
                    Email = newUser.Email,
                    CreateAt = newUser.CreateAt,
                    IsActive = newUser.IsActive
                });

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("成功创建用户: {Username}, ID: {UserId}", username, userId);
                    return newUser;
                }
                else
                {
                    _logger.LogError("创建用户失败: 数据库插入失败 - {Username}", username);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建用户时发生错误: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 密码哈希（使用Argon2id）- 保留用于将来可能的密码加密需求
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>哈希后的密码（包含盐值）</returns>
        public static string HashPassword(string password)
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
        /// 验证密码（使用Argon2id）- 保留用于将来可能的密码验证需求
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="hashedPassword">哈希后的密码（包含盐值）</param>
        /// <returns>密码是否匹配</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
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
