namespace Transportados.Platform.Core
{
    public sealed class PlatformTenantMemberClaim
    {
        public Guid TenantMemberId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public TimeSpan? WorkingHoursStart { get; set; }
        public TimeSpan? WorkingHoursEnd { get; set; }
        public double? MonthlyHoursGoal { get; set; }
    }
}
