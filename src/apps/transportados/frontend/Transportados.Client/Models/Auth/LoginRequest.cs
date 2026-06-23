using System.Text.Json.Serialization;

namespace Transportados.Client.Models.Auth;

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActiveRole { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ActiveTenantId { get; set; }

    public string? AppContext { get; set; }
}

public sealed class SelectContextRequest
{
    public string ActiveRole { get; set; } = string.Empty;
    public Guid? ActiveTenantId { get; set; }
    public string? AppContext { get; set; }
}
