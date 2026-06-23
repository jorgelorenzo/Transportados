namespace Transportados.Client.Services.Biometrics;

public enum BiometricAuthenticationStatus
{
    Succeeded,
    NotAvailable,
    NotEnrolled,
    Canceled,
    LockedOut,
    Failed,
    Error
}
