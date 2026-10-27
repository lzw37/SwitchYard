using Dapper;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwitchYard.Service.Models;
using SwitchYard.Service.Utils;
using System.Security.Cryptography;
using System.Text;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// User service for user CRUD and password operations.
    /// </summary>
    public class UserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        // Snowflake ID generator (singleton style).
        private static readonly SnowflakeIdGenerator _idGenerator =
            new SnowflakeIdGenerator(workerId: 1, datacenterId: 1);

        public UserService(ILogger<UserService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Validate login credentials and return active user.
        /// </summary>
        public User? ValidateUser(string username, string password)
        {
            try
            {
                var user = GetActiveUserByUsername(username);
                if (user == null)
                {
                    return null;
                }

                if (VerifyPassword(password, user.PasswordHash))
                {
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user login: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Get user by username (including inactive users).
        /// </summary>
        public User? GetUserByUsername(string username)
        {
            return GetUserByUsername(username, includeInactive: true);
        }

        /// <summary>
        /// Get active user by username.
        /// </summary>
        public User? GetActiveUserByUsername(string username)
        {
            return GetUserByUsername(username, includeInactive: false);
        }

        /// <summary>
        /// Get user by username.
        /// </summary>
        private User? GetUserByUsername(string username, bool includeInactive)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = includeInactive
                    ? "SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword FROM user WHERE name = @Username"
                    : "SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword FROM user WHERE name = @Username AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Username = username });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Get user by id (including inactive users).
        /// </summary>
        public User? GetUserById(string id)
        {
            return GetUserById(id, includeInactive: true);
        }

        /// <summary>
        /// Get active user by id.
        /// </summary>
        public User? GetActiveUserById(string id)
        {
            return GetUserById(id, includeInactive: false);
        }

        /// <summary>
        /// Get user by id.
        /// </summary>
        private User? GetUserById(string id, bool includeInactive)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = includeInactive
                    ? "SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword FROM user WHERE id = @Id"
                    : "SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword FROM user WHERE id = @Id AND isactive = 1";
                var users = dbConnector.Query<User>(sql, new { Id = id });
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by id: {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Create a new user.
        /// </summary>
        public User? CreateUser(
            string username,
            string password,
            string? email = null,
            string role = "User",
            uint isActive = 1,
            uint mustChangePassword = 0)
        {
            try
            {
                var existingUser = GetUserByUsername(username);
                if (existingUser != null)
                {
                    _logger.LogWarning("Create user failed: username already exists - {Username}", username);
                    return null;
                }

                var userId = _idGenerator.NextIdString();
                var hashedPassword = HashPassword(password);

                var newUser = new User
                {
                    Id = userId,
                    Name = username,
                    PasswordHash = hashedPassword,
                    Email = email,
                    Role = role,
                    CreateAt = DateTime.Now,
                    IsActive = isActive,
                    MustChangePassword = mustChangePassword
                };

                var dbConnector = DBConnector.GetDBConnector();
                var sql = @"INSERT INTO user (id, name, passwordhash, role, email, createat, isactive, mustchangepassword)
                            VALUES (@Id, @Name, @PasswordHash, @Role, @Email, @CreateAt, @IsActive, @MustChangePassword)";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = newUser.Id,
                    Name = newUser.Name,
                    PasswordHash = newUser.PasswordHash,
                    Role = newUser.Role,
                    Email = newUser.Email,
                    CreateAt = newUser.CreateAt,
                    IsActive = newUser.IsActive,
                    MustChangePassword = newUser.MustChangePassword
                });

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("User created successfully: {Username}, ID: {UserId}", username, userId);
                    return newUser;
                }

                _logger.LogError("Create user failed: database insert failed - {Username}", username);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Set user active status.
        /// </summary>
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
                _logger.LogError(ex, "Error setting user active status: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Get all users, including inactive users.
        /// </summary>
        public List<User> GetAllUsers()
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword FROM user ORDER BY createat DESC";
                return dbConnector.Query<User>(sql) ?? new List<User>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all users");
                return new List<User>();
            }
        }

        /// <summary>
        /// Get paged users, including inactive users.
        /// </summary>
        public PagedResult<User> GetUsersPage(int pageNumber, int pageSize, string? keyword = null)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var trimmedKeyword = keyword?.Trim();
                var parameters = new DynamicParameters();
                var whereSql = string.Empty;

                if (!string.IsNullOrWhiteSpace(trimmedKeyword))
                {
                    whereSql = "WHERE id LIKE @Keyword OR name LIKE @Keyword OR role LIKE @Keyword OR email LIKE @Keyword";
                    parameters.Add("Keyword", $"%{trimmedKeyword}%");
                }

                var countSql = $"SELECT COUNT(1) FROM user {whereSql}";
                var totalCount = (dbConnector.Query<int>(countSql, parameters) ?? new List<int> { 0 }).FirstOrDefault();

                parameters.Add("PageSize", pageSize);
                parameters.Add("Offset", Math.Max(0, (pageNumber - 1) * pageSize));

                var dataSql = $@"
                    SELECT id, name, passwordhash, role, email, createat, isactive, mustchangepassword
                    FROM user
                    {whereSql}
                    ORDER BY createat DESC, id DESC
                    LIMIT @PageSize OFFSET @Offset";

                var items = dbConnector.Query<User>(dataSql, parameters) ?? new List<User>();

                return new PagedResult<User>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading paged users");
                return new PagedResult<User>
                {
                    Items = Array.Empty<User>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }
        }

        /// <summary>
        /// Update all fields of a user record.
        /// </summary>
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
                                isactive = @IsActive,
                                mustchangepassword = @MustChangePassword
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
                    IsActive = updatedUser.IsActive,
                    MustChangePassword = updatedUser.MustChangePassword
                });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {CurrentUserId}", currentUserId);
                return false;
            }
        }

        /// <summary>
        /// Update user email.
        /// </summary>
        public bool UpdateUserEmail(string userId, string? email)
        {
            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var sql = "UPDATE user SET email = @Email WHERE id = @Id";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = userId,
                    Email = email
                });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user email: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Reset user password.
        /// </summary>
        public bool ResetUserPassword(string userId, string newPassword, bool forceChangeAtNextLogin = false)
        {
            try
            {
                var hashedPassword = HashPassword(newPassword);

                var dbConnector = DBConnector.GetDBConnector();
                var sql = "UPDATE user SET passwordhash = @PasswordHash, mustchangepassword = @MustChangePassword WHERE id = @Id";
                var rowsAffected = dbConnector.ExecuteNonQuery(sql, new
                {
                    Id = userId,
                    PasswordHash = hashedPassword,
                    MustChangePassword = forceChangeAtNextLogin ? 1u : 0u
                });
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user password: {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Delete user by id.
        /// </summary>
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

        /// <summary>
        /// Hash password with Argon2id and random salt.
        /// </summary>
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 8,
                MemorySize = 65536,
                Iterations = 4
            };

            var hash = argon2.GetBytes(32);
            var hashWithSalt = new byte[48];
            Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
            Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);

            return Convert.ToBase64String(hashWithSalt);
        }

        /// <summary>
        /// Verify password against stored hash (salt + hash, Base64).
        /// </summary>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                var hashWithSalt = Convert.FromBase64String(hashedPassword);

                var salt = new byte[16];
                Buffer.BlockCopy(hashWithSalt, 0, salt, 0, 16);

                var storedHash = new byte[32];
                Buffer.BlockCopy(hashWithSalt, 16, storedHash, 0, 32);

                using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
                {
                    Salt = salt,
                    DegreeOfParallelism = 8,
                    MemorySize = 65536,
                    Iterations = 4
                };

                var computedHash = argon2.GetBytes(32);
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
