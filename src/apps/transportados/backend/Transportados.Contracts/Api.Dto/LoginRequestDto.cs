namespace Transportados.Contracts.Api.Dto;

public sealed class LoginRequestDto
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? ActiveRole { get; init; }
    public Guid? ActiveTenantId { get; init; }
    public string? AppContext { get; init; }
}

public sealed class SelectContextRequestDto
{
    public string ActiveRole { get; init; } = string.Empty;
    public Guid? ActiveTenantId { get; init; }
    public string? AppContext { get; init; }
}
