namespace Transportados.Client.Services.Biometrics;

public sealed record BiometricAuthenticationRequest(
    string Title,
    string Subtitle,
    string NegativeButtonText,
    BiometricRequestReason Reason)
{
    public static BiometricAuthenticationRequest ForSessionRestore() =>
        new(
            "Confirmar identidad",
            "Usa la biometria del dispositivo para continuar.",
            "Cancelar",
            BiometricRequestReason.SessionRestore);

    public static BiometricAuthenticationRequest ForProfileOptIn() =>
        new(
            "Activar biometria",
            "Confirma tu identidad para usar biometria en proximos ingresos.",
            "Cancelar",
            BiometricRequestReason.ProfileOptIn);
}
