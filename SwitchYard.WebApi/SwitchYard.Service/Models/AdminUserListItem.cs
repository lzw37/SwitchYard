namespace SwitchYard.Service.Models
{
    public sealed class AdminUserListItem
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Role { get; init; } = "User";

        public string? Email { get; init; }

        public DateTime CreateAt { get; init; }

        public uint IsActive { get; init; }

        public uint MustChangePassword { get; init; }
    }
}
