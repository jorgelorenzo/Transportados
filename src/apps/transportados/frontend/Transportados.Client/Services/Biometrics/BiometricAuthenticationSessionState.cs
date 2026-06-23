namespace Transportados.Client.Services.Biometrics;

public sealed class BiometricAuthenticationSessionState
{
    private bool _isConfirmed;

    public bool IsConfirmed => _isConfirmed;

    public void MarkConfirmed()
    {
        _isConfirmed = true;
    }

    public void Reset()
    {
        _isConfirmed = false;
    }
}
