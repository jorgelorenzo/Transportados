using Transportados.Domain.Api.Domain;

namespace Transportados.Contracts.Api.Dto;

public sealed class TenantMemberInfoDto
{
    public Guid TenantMemberId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public sealed class DemoUserDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class DemoLoginRequestDto
{
    public Guid UserId { get; set; }
}

public sealed class RegisterCompanyAccountRequestDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactFullName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
}

public sealed class RegisterCompanyAccountResponseDto
{
    public TenantInfoDto Tenant { get; set; } = new();
    public bool RequiresApproval { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}

public sealed class TenantInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public bool IsDemo { get; set; }
}

public class QueryBaseDto
{
    public string? Search { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Phone { get; set; }
    public string? Code { get; set; }
    public string? Latitud { get; set; }
    public string? Longitud { get; set; }
    public string? Notes { get; set; }
}

public sealed class CustomerListResponseDto
{
    public int Total { get; set; }
    public List<CustomerDto> Customers { get; set; } = [];
}

public sealed class CustomerQueryDto : QueryBaseDto
{
    public string? CityFilter { get; set; }
    public string? StateFilter { get; set; }
}

public static class CustomerSortFields
{
    public const string FullName = "fullName";
    public const string Code = "code";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string City = "city";
}

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public bool IsSuperAdmin { get; set; }
    public List<TenantMemberInfoDto> TenantMemberships { get; set; } = [];
}

public sealed class UserSaveRequestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Role { get; set; } = Roles.Tech;
    public Guid? TenantId { get; set; }
    public string? Photo { get; set; }
}

public sealed class UserListResponseDto
{
    public int Total { get; set; }
    public List<UserDto> Users { get; set; } = [];
}

public sealed class UserQueryDto : QueryBaseDto
{
}

public sealed class SettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Name { get; set; }
    public string? NameOfficeOne { get; set; }
    public string? AddressOfficeOne { get; set; }
    public string? ContactOfficeOne { get; set; }
    public string? NameOfficeTwo { get; set; }
    public string? AddressOfficeTwo { get; set; }
    public string? ContactOfficeTwo { get; set; }
    public string? Highlighted { get; set; }
    public string? LogoImage { get; set; }
    public string? LogoMenu { get; set; }
    public string? AppTheme { get; set; }
    public string TechRoleLabel { get; set; } = Roles.DefaultTechRoleLabel;
    public int SmtpPort { get; set; }
    public string? SmtpHost { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public bool SmtpUseSSL { get; set; }
    public string? EmailFrom { get; set; }
    public string? SendWOCopyTo { get; set; }
    public bool IsSmtpEnabled { get; set; }
}

public class TenantListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public bool IsDemo { get; set; }
    public string? RegistrationContactFullName { get; set; }
    public string? RegistrationContactEmail { get; set; }
    public string? RegistrationContactPhone { get; set; }
}

public sealed class TenantFeatureFlagsDto
{
    public bool Appearance { get; set; } = true;
    public bool Email { get; set; } = true;
}

public sealed class TenantDetailDto : TenantListItemDto
{
    public TenantFeatureFlagsDto Features { get; set; } = new();
}

public sealed class TenantUpdateRequestDto
{
    public TenantStatus? Status { get; set; }
    public TenantFeatureFlagsDto? Features { get; set; }
}

public sealed class TenantPhysicalDeleteRequestDto
{
    public string ConfirmationTenantName { get; set; } = string.Empty;
}

public sealed class PlatformStatsDto
{
    public int TotalTenants { get; set; }
    public int PendingTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int DisabledTenants { get; set; }
    public int TotalUsers { get; set; }
    public int TotalCustomers { get; set; }
}
