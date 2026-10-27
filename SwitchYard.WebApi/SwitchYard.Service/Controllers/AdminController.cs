using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using System.Security.Cryptography;
using System.Text;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly HumpInstanceCopyService _humpInstanceCopyService;
        private readonly ILogger<AdminController> _logger;
        private const string DefaultTemplateInstanceId = "001";

        public AdminController(
            UserService userService,
            HumpInstanceCopyService humpInstanceCopyService,
            ILogger<AdminController> logger)
        {
            _userService = userService;
            _humpInstanceCopyService = humpInstanceCopyService;
            _logger = logger;
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            try
            {
                var users = _userService.GetAllUsers()
                    .Select(user => new
                    {
                        id = user.Id,
                        name = user.Name,
                        role = user.Role,
                        email = user.Email,
                        createAt = user.CreateAt,
                        isActive = user.IsActive,
                        mustChangePassword = user.MustChangePassword
                    });

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user list");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("users/paged")]
        public IActionResult GetUsersPaged([FromQuery] UserPaginationQuery query)
        {
            try
            {
                var pagedUsers = _userService.GetUsersPage(query.PageNumber, query.PageSize, query.Keyword);
                var result = new PagedResult<AdminUserListItem>
                {
                    Items = pagedUsers.Items.Select(user => new AdminUserListItem
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Role = user.Role,
                        Email = user.Email,
                        CreateAt = user.CreateAt,
                        IsActive = user.IsActive,
                        MustChangePassword = user.MustChangePassword
                    }).ToList(),
                    PageNumber = pagedUsers.PageNumber,
                    PageSize = pagedUsers.PageSize,
                    TotalCount = pagedUsers.TotalCount
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get paged user list");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] AdminCreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Username and password cannot be empty" });
                }

                if (request.IsActive != 0 && request.IsActive != 1)
                {
                    return BadRequest(new { message = "isActive must be 0 or 1" });
                }

                var normalizedRole = NormalizeRole(request.Role);
                if (normalizedRole == null)
                {
                    return BadRequest(new { message = "Invalid role. Allowed values are User or Admin." });
                }

                var username = request.Username.Trim();
                var existingUser = _userService.GetUserByUsername(username);
                if (existingUser != null)
                {
                    return Conflict(new { message = "Username already exists" });
                }

                var createdUser = _userService.CreateUser(
                    username,
                    request.Password.Trim(),
                    string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    normalizedRole,
                    request.IsActive,
                    mustChangePassword: 1
                );

                if (createdUser == null)
                {
                    return StatusCode(500, new { message = "Failed to create user" });
                }

                var defaultInstanceCopyResult = _humpInstanceCopyService.CopyTemplateInstanceForNewUser(
                    DefaultTemplateInstanceId,
                    createdUser.Name);
                if (!defaultInstanceCopyResult.Success)
                {
                    _logger.LogError(
                        "Default hump instance initialization failed for admin-created user {Username}, UserId: {UserId}, TemplateInstanceId: {TemplateInstanceId}, StatusCode: {StatusCode}, Error: {ErrorMessage}",
                        createdUser.Name,
                        createdUser.Id,
                        DefaultTemplateInstanceId,
                        defaultInstanceCopyResult.StatusCode,
                        defaultInstanceCopyResult.ErrorMessage);

                    var rollbackSucceeded = _userService.DeleteUser(createdUser.Id);
                    if (!rollbackSucceeded)
                    {
                        _logger.LogError(
                            "User rollback failed after default hump instance initialization error for admin-created user {Username}, UserId: {UserId}",
                            createdUser.Name,
                            createdUser.Id);
                        return StatusCode(500, new { message = "Failed to initialize default instance and failed to roll back user creation" });
                    }

                    return StatusCode(500, new { message = "Failed to initialize default instance" });
                }

                return Ok(new
                {
                    id = createdUser.Id,
                    name = createdUser.Name,
                    role = createdUser.Role,
                    email = createdUser.Email,
                    createAt = createdUser.CreateAt,
                    isActive = createdUser.IsActive,
                    mustChangePassword = createdUser.MustChangePassword
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("users/{id}")]
        public IActionResult UpdateUser([FromRoute] string id, [FromBody] AdminUpdateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                var currentUser = _userService.GetUserById(id);
                if (currentUser == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (string.IsNullOrWhiteSpace(request.Id) ||
                    string.IsNullOrWhiteSpace(request.Name) ||
                    string.IsNullOrWhiteSpace(request.Role))
                {
                    return BadRequest(new { message = "Request parameters are invalid. User ID, name, and role cannot be empty." });
                }

                if (request.IsActive != 0 && request.IsActive != 1)
                {
                    return BadRequest(new { message = "isActive must be 0 or 1" });
                }

                var normalizedRole = NormalizeRole(request.Role);
                if (normalizedRole == null)
                {
                    return BadRequest(new { message = "Invalid role. Allowed values are User or Admin." });
                }

                var newUserId = request.Id.Trim();
                var newUsername = request.Name.Trim();

                if (!string.Equals(currentUser.Id, newUserId, StringComparison.Ordinal))
                {
                    var userById = _userService.GetUserById(newUserId);
                    if (userById != null)
                    {
                        return Conflict(new { message = "User ID already exists" });
                    }
                }

                if (!string.Equals(currentUser.Name, newUsername, StringComparison.OrdinalIgnoreCase))
                {
                    var userByName = _userService.GetUserByUsername(newUsername);
                    if (userByName != null)
                    {
                        return Conflict(new { message = "Username already exists" });
                    }
                }

                var updatedUser = new User
                {
                    Id = newUserId,
                    Name = newUsername,
                    PasswordHash = currentUser.PasswordHash,
                    Role = normalizedRole,
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    CreateAt = request.CreateAt == default ? currentUser.CreateAt : request.CreateAt,
                    IsActive = request.IsActive,
                    MustChangePassword = currentUser.MustChangePassword
                };

                var updated = _userService.UpdateUser(currentUser.Id, updatedUser);
                if (!updated)
                {
                    return StatusCode(500, new { message = "Failed to update user" });
                }

                return Ok(new
                {
                    id = updatedUser.Id,
                    name = updatedUser.Name,
                    role = updatedUser.Role,
                    email = updatedUser.Email,
                    createAt = updatedUser.CreateAt,
                    isActive = updatedUser.IsActive,
                    mustChangePassword = updatedUser.MustChangePassword
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("users/{id}/reset-password")]
        public IActionResult ResetUserPassword([FromRoute] string id, [FromBody] AdminResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                if (string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new { message = "New password cannot be empty" });
                }

                var user = _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var updated = _userService.ResetUserPassword(user.Id, request.NewPassword.Trim(), forceChangeAtNextLogin: true);
                if (!updated)
                {
                    return StatusCode(500, new { message = "Failed to reset password" });
                }

                return Ok(new { message = "Password reset successfully. User must change password at next login." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset password: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser([FromRoute] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest(new { message = "User ID cannot be empty" });
                }

                var user = _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var currentUsername = User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(currentUsername) &&
                    string.Equals(currentUsername, user.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "You cannot delete the currently logged-in account" });
                }

                if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var adminCount = _userService
                        .GetAllUsers()
                        .Count(u => string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase));

                    if (adminCount <= 1)
                    {
                        return BadRequest(new { message = "Cannot delete the last admin account" });
                    }
                }

                var deleted = _userService.DeleteUser(user.Id);
                if (!deleted)
                {
                    return StatusCode(500, new { message = "Failed to delete user" });
                }

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user: {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpGet("users/import-template")]
        public IActionResult DownloadImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Users");

            // Header row
            ws.Cell(1, 1).Value = "username";
            ws.Cell(1, 2).Value = "password";
            ws.Cell(1, 3).Value = "role";
            ws.Cell(1, 4).Value = "email";
            ws.Cell(1, 5).Value = "isActive";

            // Style headers
            var headerRange = ws.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Example row
            ws.Cell(2, 1).Value = "zhangsan";
            ws.Cell(2, 2).Value = "Password123";
            ws.Cell(2, 3).Value = "User";
            ws.Cell(2, 4).Value = "zhangsan@example.com";
            ws.Cell(2, 5).Value = 1;

            ws.Cell(3, 1).Value = "lisi_admin";
            ws.Cell(3, 2).Value = "Admin@456";
            ws.Cell(3, 3).Value = "Admin";
            ws.Cell(3, 4).Value = "";
            ws.Cell(3, 5).Value = 1;

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "user_info_template.xlsx"
            );
        }

        [HttpPost("users/import")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit
        public async Task<IActionResult> ImportUsers(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "未上传文件" });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "仅支持 .xlsx 格式" });

            var results = new List<object>();
            int successCount = 0, failedCount = 0;

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                // Parse header row to find column indices (case-insensitive)
                var headerRow = worksheet.Row(1);
                var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= headerRow.LastCellUsed()?.Address.ColumnNumber; col++)
                {
                    var header = headerRow.Cell(col).GetString().Trim();
                    if (!string.IsNullOrEmpty(header))
                        colIndex[header] = col;
                }

                string GetCell(IXLRow row, string name, int fallback)
                {
                    return colIndex.TryGetValue(name, out var idx)
                        ? row.Cell(idx).GetString().Trim()
                        : row.Cell(fallback).GetString().Trim();
                }

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                for (int rowNum = 2; rowNum <= lastRow; rowNum++)
                {
                    var wsRow = worksheet.Row(rowNum);

                    var username = GetCell(wsRow, "username", 1);
                    var password = GetCell(wsRow, "password", 2);
                    var role = GetCell(wsRow, "role", 3);
                    var email = GetCell(wsRow, "email", 4);
                    var isActiveStr = GetCell(wsRow, "isActive", 5);

                    // Skip completely empty rows
                    if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
                        continue;

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "用户名和密码不能为空" });
                        continue;
                    }

                    if (password.Length < 6)
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "密码长度至少为 6 位" });
                        continue;
                    }

                    var normalizedRole = NormalizeRole(string.IsNullOrEmpty(role) ? "User" : role);
                    if (normalizedRole == null)
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "角色无效，必须为 User 或 Admin" });
                        continue;
                    }

                    uint isActive = 1;
                    if (!string.IsNullOrEmpty(isActiveStr) &&
                        (!uint.TryParse(isActiveStr, out isActive) || (isActive != 0 && isActive != 1)))
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "激活状态必须为 0 或 1" });
                        continue;
                    }

                    var existingUser = _userService.GetUserByUsername(username);
                    if (existingUser != null)
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "用户名已存在" });
                        continue;
                    }

                    // SHA-256 hash the plaintext password, matching what the frontend sends
                    var sha256Bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    var sha256Hex = Convert.ToHexString(sha256Bytes).ToLowerInvariant();

                    var createdUser = _userService.CreateUser(
                        username,
                        sha256Hex,
                        string.IsNullOrEmpty(email) ? null : email,
                        normalizedRole,
                        isActive,
                        mustChangePassword: 1
                    );

                    if (createdUser == null)
                    {
                        failedCount++;
                        results.Add(new { row = rowNum, username, success = false, error = "创建用户失败" });
                    }
                    else
                    {
                        var instanceResult = _humpInstanceCopyService.CopyTemplateInstanceForNewUser(
                            DefaultTemplateInstanceId,
                            createdUser.Name);

                        if (!instanceResult.Success)
                        {
                            _logger.LogError(
                                "Default hump instance init failed for imported user {Username}, StatusCode: {StatusCode}, Error: {ErrorMessage}",
                                createdUser.Name,
                                instanceResult.StatusCode,
                                instanceResult.ErrorMessage);

                            _userService.DeleteUser(createdUser.Id);
                            failedCount++;
                            results.Add(new { row = rowNum, username, success = false, error = $"创建默认实例失败：{instanceResult.ErrorMessage}" });
                        }
                        else
                        {
                            successCount++;
                            results.Add(new { row = rowNum, username, success = true, error = (string?)null });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import users from Excel");
                return StatusCode(500, new { message = "解析文件失败：" + ex.Message });
            }

            return Ok(new
            {
                totalCount = successCount + failedCount,
                successCount,
                failedCount,
                results
            });
        }

        private static string? NormalizeRole(string role)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "User";
            }

            return null;
        }
    }
}

