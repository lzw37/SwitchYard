using SwitchYard.Service.Models;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// 鐢ㄦ埛鏈嶅姟
    /// </summary>
    public class UserService
    {
        private readonly ILogger<UserService> _logger;

        // 闆姳绠楁硶ID鐢熸垚鍣紙闈欐€佸崟渚嬶紝workerId=1, datacenterId=1锛?
        private static readonly SnowflakeIdGenerator _idGenerator = new SnowflakeIdGenerator(workerId: 1, datacenterId: 1);

        public UserService(ILogger<UserService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 楠岃瘉鐢ㄦ埛鐧诲綍
        /// </summary>
        /// <param name="username">鐢ㄦ埛鍚?/param>
        /// <param name="password">瀵嗙爜</param>
        /// <returns>鐢ㄦ埛淇℃伅锛屽鏋滈獙璇佸け璐ュ垯杩斿洖null</returns>
        public User? ValidateUser(string username, string password)
        {
            try
            {
                var user = GetActiveUserByUsername(username);
                if (user == null)
                {
                    return null;
                }

                // 浣跨敤Argon2id楠岃瘉瀵嗙爜
                if (VerifyPassword(password, user.PasswordHash))
                {
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "楠岃瘉鐢ㄦ埛鏃跺彂鐢熼敊璇? {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 鏍规嵁鐢ㄦ埛鍚嶈幏鍙栫敤鎴?
        /// </summary>
        /// <param name="username">鐢ㄦ埛鍚?/param>
        /// <returns>鐢ㄦ埛淇℃伅</returns>
        public User? GetUserByUsername(string username)
        {
            return GetUserByUsername(username, includeInactive: true);
        }

        /// <summary>
        /// 閺嶈宓侀悽銊﹀煕閸氬秷骞忛崣鏍у嚒濠碘偓濞茶崵鏁ら幋?
        /// </summary>
        /// <param name="username">閻劍鍩涢崥?/param>
        /// <returns>瀹稿弶绺哄ú鑽ゆ暏閹磋渹淇婇幁?/returns>
        public User? GetActiveUserByUsername(string username)
        {
            return GetUserByUsername(username, includeInactive: false);
        }

        /// <summary>
        /// 閺嶈宓侀悽銊﹀煕閸氬秷骞忛崣鏍暏閹?
        /// </summary>
        /// <param name="username">閻劍鍩涢崥?/param>
        /// <param name="includeInactive">閺勵垰鎯侀崠鍛儓閺堫亝绺哄ú鑽ゆ暏閹?/param>
        /// <returns>閻劍鍩涙穱鈩冧紖</returns>
        private User? GetUserByUsername(string username, bool includeInactive)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = includeInactive
                    ? "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE name = @Username"
                    : "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE name = @Username AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Username = username });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "鏍规嵁鐢ㄦ埛鍚嶈幏鍙栫敤鎴锋椂鍙戠敓閿欒: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 鏍规嵁ID鑾峰彇鐢ㄦ埛
        /// </summary>
        /// <param name="id">鐢ㄦ埛ID</param>
        /// <returns>鐢ㄦ埛淇℃伅</returns>
        public User? GetUserById(string id)
        {
            return GetUserById(id, includeInactive: true);
        }

        /// <summary>
        /// 閺嶈宓両D閼惧嘲褰囬悽銊﹀煕
        /// </summary>
        /// <param name="id">閻劍鍩汭D</param>
        /// <param name="includeInactive">閺勵垰鎯侀崠鍛儓閺堫亝绺哄ú鑽ゆ暏閹?/param>
        /// <returns>閻劍鍩涙穱鈩冧紖</returns>
        private User? GetUserById(string id, bool includeInactive)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = includeInactive
                    ? "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE id = @Id"
                    : "SELECT id, name, passwordhash, role, email, createat, isactive FROM user WHERE id = @Id AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Id = id });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "鏍规嵁ID鑾峰彇鐢ㄦ埛鏃跺彂鐢熼敊璇? {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// 鍒涘缓鏂扮敤鎴?
        /// </summary>
        /// <param name="username">鐢ㄦ埛鍚?/param>
        /// <param name="password">瀵嗙爜锛堝皢琚姞瀵嗗瓨鍌級</param>
        /// <param name="email">閭</param>
        /// <param name="role">瑙掕壊</param>
        /// <returns>鍒涘缓鐨勭敤鎴蜂俊鎭紝澶辫触杩斿洖null</returns>
        public User? CreateUser(string username, string password, string? email = null, string role = "User", uint isActive = 1)
        {
            try
            {
                // 妫€鏌ョ敤鎴峰悕鏄惁宸插瓨鍦?
                var existingUser = GetUserByUsername(username);
                if (existingUser != null)
                {
                    _logger.LogWarning("鍒涘缓鐢ㄦ埛澶辫触: 鐢ㄦ埛鍚嶅凡瀛樺湪 - {Username}", username);
                    return null;
                }

                // 浣跨敤闆姳绠楁硶鐢熸垚鏂扮殑鐢ㄦ埛ID
                var userId = _idGenerator.NextIdString();

                // 浣跨敤Argon2id瀵瑰瘑鐮佽繘琛屽搱甯屽姞瀵?
                var hashedPassword = HashPassword(password);

                // 鍒涘缓鐢ㄦ埛瀵硅薄
                var newUser = new User
                {
                    Id = userId,
                    Name = username,
                    PasswordHash = hashedPassword, // 瀛樺偍鍔犲瘑鍚庣殑瀵嗙爜
                    Email = email,
                    Role = role,
                    CreateAt = DateTime.Now,
                    IsActive = isActive
                };

                // 鎻掑叆鏁版嵁搴?
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
                    _logger.LogInformation("鎴愬姛鍒涘缓鐢ㄦ埛: {Username}, ID: {UserId}", username, userId);
                    return newUser;
                }
                else
                {
                    _logger.LogError("鍒涘缓鐢ㄦ埛澶辫触: 鏁版嵁搴撴彃鍏ュけ璐?- {Username}", username);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "鍒涘缓鐢ㄦ埛鏃跺彂鐢熼敊璇? {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// 閺囧瓨鏌婇悽銊﹀煕濠碘偓濞茶崵濮搁幀?
        /// </summary>
        /// <param name="userId">閻劍鍩汭D</param>
        /// <param name="isActive">濠碘偓濞茶崵濮搁幀渚婄礄1=濠碘偓濞蹭紮绱?=閺堫亝绺哄ú浼欑礆</param>
        /// <returns>閺勵垰鎯侀弴瀛樻煀閹存劕濮?/returns>
        public bool SetUserActiveStatus(string userId, uint isActive)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "UPDATE user SET isactive = @IsActive WHERE id = @Id";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = userId,
                    IsActive = isActive
                });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "閺囧瓨鏌婇悽銊﹀煕濠碘偓濞茶崵濮搁幀浣规閸欐垹鏁撻柨娆掝嚖: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// 瀵嗙爜鍝堝笇锛堜娇鐢ˋrgon2id锛? 淇濈暀鐢ㄤ簬灏嗘潵鍙兘鐨勫瘑鐮佸姞瀵嗛渶姹?
        /// </summary>
        /// <param name="password">鏄庢枃瀵嗙爜</param>
        /// <returns>鍝堝笇鍚庣殑瀵嗙爜锛堝寘鍚洂鍊硷級</returns>
        /// <summary>
        /// 鑾峰彇鍏ㄩ儴鐢ㄦ埛锛堝寘鍚湭婵€娲荤敤鎴凤級
        /// </summary>
        /// <returns>鐢ㄦ埛鍒楄〃</returns>
        public List<User> GetAllUsers()
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "SELECT id, name, passwordhash, role, email, createat, isactive FROM user ORDER BY createat DESC";
                return dbConnector.Query<User>(sql) ?? new List<User>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading all users");
                return new List<User>();
            }
        }

        /// <summary>
        /// 鎸夌収绠＄悊鍛樻彁浜ょ殑鏁版嵁鏇存柊鐢ㄦ埛鍏ㄩ儴瀛楁
        /// </summary>
        /// <param name="currentUserId">褰撳墠鐢ㄦ埛ID锛堟洿鏂板墠锛?/param>
        /// <param name="updatedUser">鏇存柊鍚庣殑鐢ㄦ埛瀵硅薄</param>
        /// <returns>鏄惁鏇存柊鎴愬姛</returns>
        public bool UpdateUser(string currentUserId, User updatedUser)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = @"UPDATE user 
                            SET id = @Id, 
                                name = @Name, 
                                passwordhash = @PasswordHash, 
                                role = @Role, 
                                email = @Email, 
                                createat = @CreateAt, 
                                isactive = @IsActive
                            WHERE id = @CurrentUserId";

                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    CurrentUserId = currentUserId,
                    Id = updatedUser.Id,
                    Name = updatedUser.Name,
                    PasswordHash = updatedUser.PasswordHash,
                    Role = updatedUser.Role,
                    Email = updatedUser.Email,
                    CreateAt = updatedUser.CreateAt,
                    IsActive = updatedUser.IsActive
                });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "鏇存柊鐢ㄦ埛鏃跺彂鐢熼敊璇? {CurrentUserId}", currentUserId);
                return false;
            }
        }

        /// <summary>
        /// 閲嶇疆鎸囧畾鐢ㄦ埛瀵嗙爜
        /// </summary>
        /// <param name="userId">鐢ㄦ埛ID</param>
        /// <param name="newPassword">鏂板瘑鐮侊紙鏄庢枃鎴栦笂娓告憳瑕佸瓧绗︿覆锛?/param>
        /// <returns>鏄惁鏇存柊鎴愬姛</returns>
        public bool ResetUserPassword(string userId, string newPassword)
        {
            try
            {
                var hashedPassword = HashPassword(newPassword);

                var dbConnector = DBConnector.GetDBConnector();
                var sql = "UPDATE user SET passwordhash = @PasswordHash WHERE id = @Id";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = userId,
                    PasswordHash = hashedPassword
                });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "閲嶇疆鐢ㄦ埛瀵嗙爜鏃跺彂鐢熼敊璇? {UserId}", userId);
                return false;
            }
        }
        public bool DeleteUser(string userId)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "DELETE FROM user WHERE id = @Id";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new { Id = userId });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting user: {UserId}", userId);
                return false;
            }
        }
        public static string HashPassword(string password)
        {
            // 鐢熸垚16瀛楄妭鐨勯殢鏈虹洂鍊?
            var salt = RandomNumberGenerator.GetBytes(16);

            // 浣跨敤Argon2id杩涜瀵嗙爜鍝堝笇
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 8,      // 骞惰搴?
                MemorySize = 65536,  // 鍐呭瓨浣跨敤閲忥紙64 MB锛?
                Iterations = 4  // 杩唬娆℃暟
            };

            // 鐢熸垚32瀛楄妭鐨勫搱甯屽€?
            var hash = argon2.GetBytes(32);

            // 灏嗙洂鍊煎拰鍝堝笇鍊肩粍鍚堝瓨鍌細鐩愬€?16瀛楄妭) + 鍝堝笇鍊?32瀛楄妭)
            var hashWithSalt = new byte[48];
            Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
            Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);

            // 杩斿洖Base64缂栫爜鐨勫瓧绗︿覆
            return Convert.ToBase64String(hashWithSalt);
        }

        /// <summary>
        /// 楠岃瘉瀵嗙爜锛堜娇鐢ˋrgon2id锛? 淇濈暀鐢ㄤ簬灏嗘潵鍙兘鐨勫瘑鐮侀獙璇侀渶姹?
        /// </summary>
        /// <param name="password">鏄庢枃瀵嗙爜</param>
        /// <param name="hashedPassword">鍝堝笇鍚庣殑瀵嗙爜锛堝寘鍚洂鍊硷級</param>
        /// <returns>瀵嗙爜鏄惁鍖归厤</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                // 瑙ｇ爜瀛樺偍鐨勫搱甯屽€?
                var hashWithSalt = Convert.FromBase64String(hashedPassword);

                // 鎻愬彇鐩愬€硷紙鍓?6瀛楄妭锛?
                var salt = new byte[16];
                Buffer.BlockCopy(hashWithSalt, 0, salt, 0, 16);

                // 鎻愬彇鍝堝笇鍊硷紙鍚?2瀛楄妭锛?
                var storedHash = new byte[32];
                Buffer.BlockCopy(hashWithSalt, 16, storedHash, 0, 32);

                // 浣跨敤鐩稿悓鐨勫弬鏁板拰鐩愬€奸噸鏂拌绠楀搱甯?
                using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
                {
                    Salt = salt,
                    DegreeOfParallelism = 8,
                    MemorySize = 65536,
                    Iterations = 4
                };

                var computedHash = argon2.GetBytes(32);

                // 浣跨敤鎭掑畾鏃堕棿姣旇緝闃叉鏃堕棿鏀诲嚮
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}

