using Microsoft.Extensions.Logging;
using SwitchYard.Hump;

namespace SwitchYard.Service.Services
{
    /// <summary>
    /// 实例授权服务 - 用于验证用户是否为实例所有者
    /// </summary>
    public class InstanceAuthorizationService
    {
        private readonly ILogger<InstanceAuthorizationService> _logger;

        public InstanceAuthorizationService(ILogger<InstanceAuthorizationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 验证用户是否为指定实例的所有者
        /// </summary>
        /// <param name="instanceID">实例ID</param>
        /// <param name="username">当前用户名</param>
        /// <returns>授权结果</returns>
        public InstanceAuthorizationResult ValidateInstanceOwnership(string instanceID, string? username)
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Authorization failed: username is null or empty.");
                return InstanceAuthorizationResult.Unauthorized("User not authenticated.");
            }

            if (string.IsNullOrEmpty(instanceID))
            {
                _logger.LogWarning("Authorization failed: instanceID is null or empty.");
                return InstanceAuthorizationResult.NotFound("Instance ID is required.");
            }

            try
            {
                var dbConnector = DBConnector.GetDBConnector();
                var instance = dbConnector.Query<HumpInstance>(
                    "SELECT * FROM humpinstance WHERE ID = @instanceID", 
                    new { instanceID }).FirstOrDefault();

                if (instance == null)
                {
                    _logger.LogWarning("Instance not found: {InstanceID}", instanceID);
                    return InstanceAuthorizationResult.NotFound("Instance not found.");
                }

                if (instance.Owner != username)
                {
                    _logger.LogWarning("User {Username} is not the owner of instance {InstanceID}.", username, instanceID);
                    return InstanceAuthorizationResult.Unauthorized("Instance not owned by user.");
                }

                _logger.LogDebug("User {Username} authorized for instance {InstanceID}.", username, instanceID);
                return InstanceAuthorizationResult.Success(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating instance ownership for instance {InstanceID}.", instanceID);
                return InstanceAuthorizationResult.Error("Internal error while validating instance ownership.");
            }
        }

        /// <summary>
        /// 获取实例（如果用户是所有者）
        /// </summary>
        /// <param name="instanceID">实例ID</param>
        /// <param name="username">当前用户名</param>
        /// <returns>实例对象，如果验证失败则返回null</returns>
        public HumpInstance? GetInstanceIfOwner(string instanceID, string? username)
        {
            var result = ValidateInstanceOwnership(instanceID, username);
            return result.Instance;
        }

        /// <summary>
        /// 检查用户是否为实例所有者
        /// </summary>
        /// <param name="instanceID">实例ID</param>
        /// <param name="username">当前用户名</param>
        /// <returns>是否为所有者</returns>
        public bool IsInstanceOwner(string instanceID, string? username)
        {
            var result = ValidateInstanceOwnership(instanceID, username);
            return result.IsAuthorized;
        }
    }

    /// <summary>
    /// 实例授权结果
    /// </summary>
    public class InstanceAuthorizationResult
    {
        /// <summary>
        /// 是否授权成功
        /// </summary>
        public bool IsAuthorized { get; private set; }

        /// <summary>
        /// 是否为未找到状态
        /// </summary>
        public bool IsNotFound { get; private set; }

        /// <summary>
        /// 是否为内部错误
        /// </summary>
        public bool IsError { get; private set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// 验证通过的实例对象
        /// </summary>
        public HumpInstance? Instance { get; private set; }

        private InstanceAuthorizationResult() { }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static InstanceAuthorizationResult Success(HumpInstance instance)
        {
            return new InstanceAuthorizationResult
            {
                IsAuthorized = true,
                Instance = instance
            };
        }

        /// <summary>
        /// 创建未授权结果
        /// </summary>
        public static InstanceAuthorizationResult Unauthorized(string message)
        {
            return new InstanceAuthorizationResult
            {
                IsAuthorized = false,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// 创建未找到结果
        /// </summary>
        public static InstanceAuthorizationResult NotFound(string message)
        {
            return new InstanceAuthorizationResult
            {
                IsAuthorized = false,
                IsNotFound = true,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// 创建错误结果
        /// </summary>
        public static InstanceAuthorizationResult Error(string message)
        {
            return new InstanceAuthorizationResult
            {
                IsAuthorized = false,
                IsError = true,
                ErrorMessage = message
            };
        }
    }
}
