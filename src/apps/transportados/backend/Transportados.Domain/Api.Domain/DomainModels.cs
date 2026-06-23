using Transportados.Platform.Core;

namespace Transportados.Domain.Api.Domain;

public class Identity
{
    public Guid Id { get; set; }
    public bool Deleted { get; set; }
}

public interface ITenant : ITransportadosTenantEntity
{
}

public enum TenantStatus
{
    Pending = 0,
    Active = 1,
    Disabled = 2
}

public enum TenantFeatureFlag
{
    Appearance,
    Email
}

public sealed class Tenant : Identity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Pending;
    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RegistrationContactFullName { get; set; }
    public string? RegistrationContactEmail { get; set; }
    public string? RegistrationContactPhone { get; set; }
    public string? RegistrationAddressLine { get; set; }
    public string? RegistrationCity { get; set; }
    public string? LogoImage { get; set; }
    public string? LogoMenu { get; set; }
    public string? ThemeConfig { get; set; }
    public bool FeatureAppearanceEnabled { get; set; } = true;
    public bool FeatureEmailEnabled { get; set; } = true;
    public bool IsActive => Status == TenantStatus.Active;
}

public static class Roles
{
    public const string SuperAdmin = "superadmin";
    public const string Admin = "admin";
    public const string Supervisor = "supervisor";
    public const string Tech = "tech";
    public const string LegacyTechnician = "technician";
    public const string LegacyOperator = "operator";
    public const string DefaultTechRoleLabel = "Operador";

    public static readonly string[] List = [SuperAdmin, Admin, Supervisor, Tech];

    public static bool IsTenantRole(string? role) =>
        role is Admin or Supervisor or Tech;

    public static string? Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var normalized = role.Trim().ToLowerInvariant();
        return normalized is LegacyOperator or LegacyTechnician ? Tech : normalized;
    }
}

public sealed class User : Identity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DefaultRole { get; set; }
    public Guid? LastActiveTenantId { get; set; }
    public string ListText => $"{FullName} ({Email})";
}

public sealed class UserPassword : Identity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string HashedPassword { get; set; } = string.Empty;
}

public sealed class UserVerificationCode : Identity
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public sealed class DemoUser : Identity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string PlainPassword { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class DemoTenantCredential : Identity, ITenant
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PlainPassword { get; set; } = string.Empty;
}

public sealed class TenantMember : Identity, ITenant
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Customer : Identity, ITenant
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
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
    public string FullAddress => $"{AddressLine1} {AddressLine2} {City} {State}".Trim();
}

public sealed class Settings : Identity, ITenant
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
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
    public bool IsSmtpEnabled => !string.IsNullOrEmpty(SmtpHost) &&
        !string.IsNullOrEmpty(SmtpUser) &&
        !string.IsNullOrEmpty(SmtpPass) &&
        SmtpPort > 0;
}
