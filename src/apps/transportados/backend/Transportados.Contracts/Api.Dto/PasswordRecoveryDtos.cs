namespace Transportados.Contracts.Api.Dto;

public sealed class PasswordRecoveryCodeRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string? PublicOrigin { get; set; }
}

public sealed class ChangePasswordWithCodeRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
