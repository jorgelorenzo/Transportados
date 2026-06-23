using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Java.Lang;
using Microsoft.Maui.ApplicationModel;
using Transportados.Client.Services.Biometrics;

namespace Transportados.Mobile.Services;

public sealed class MauiBiometricAuthenticationService : IBiometricAuthenticationService
{
    private const int AllowedAuthenticators =
        BiometricManager.Authenticators.BiometricStrong |
        BiometricManager.Authenticators.BiometricWeak;

    public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? Android.App.Application.Context;
        var biometricManager = BiometricManager.From(context);

        return Task.FromResult(MapAvailability(biometricManager.CanAuthenticate(AllowedAuthenticators)));
    }

    public async Task<BiometricAuthenticationResult> AuthenticateAsync(
        BiometricAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new BiometricAuthenticationResult(BiometricAuthenticationStatus.Canceled);
        }

        return await MainThread.InvokeOnMainThreadAsync(() =>
            AuthenticateOnMainThreadAsync(request, cancellationToken));
    }

    private static async Task<BiometricAuthenticationResult> AuthenticateOnMainThreadAsync(
        BiometricAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as FragmentActivity;
        if (activity is null)
        {
            return new BiometricAuthenticationResult(
                BiometricAuthenticationStatus.Error,
                "No se pudo abrir el prompt biometrico.");
        }

        var availability = MapAvailability(
            BiometricManager.From(activity).CanAuthenticate(AllowedAuthenticators));
        if (availability != BiometricAvailability.Available)
        {
            return new BiometricAuthenticationResult(MapUnavailableStatus(availability));
        }

        var completion = new TaskCompletionSource<BiometricAuthenticationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new AuthenticationCallback(completion);
        var executor = ContextCompat.GetMainExecutor(activity);
        if (executor is null)
        {
            return new BiometricAuthenticationResult(
                BiometricAuthenticationStatus.Error,
                "No se pudo iniciar el prompt biometrico.");
        }

        var prompt = new BiometricPrompt(activity, executor, callback);
        using var registration = cancellationToken.Register(() =>
        {
            prompt.CancelAuthentication();
            completion.TrySetResult(new BiometricAuthenticationResult(BiometricAuthenticationStatus.Canceled));
        });

        var promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle(request.Title)
            .SetSubtitle(request.Subtitle)
            .SetNegativeButtonText(request.NegativeButtonText)
            .SetAllowedAuthenticators(AllowedAuthenticators)
            .Build();

        prompt.Authenticate(promptInfo);
        return await completion.Task;
    }

    private static BiometricAvailability MapAvailability(int biometricManagerStatus) =>
        biometricManagerStatus switch
        {
            BiometricManager.BiometricSuccess => BiometricAvailability.Available,
            BiometricManager.BiometricErrorNoneEnrolled => BiometricAvailability.NotEnrolled,
            BiometricManager.BiometricErrorHwUnavailable => BiometricAvailability.TemporarilyUnavailable,
            BiometricManager.BiometricErrorNoHardware => BiometricAvailability.NotAvailable,
            _ => BiometricAvailability.Unknown
        };

    private static BiometricAuthenticationStatus MapUnavailableStatus(BiometricAvailability availability) =>
        availability switch
        {
            BiometricAvailability.NotEnrolled => BiometricAuthenticationStatus.NotEnrolled,
            BiometricAvailability.NotAvailable => BiometricAuthenticationStatus.NotAvailable,
            BiometricAvailability.TemporarilyUnavailable => BiometricAuthenticationStatus.NotAvailable,
            _ => BiometricAuthenticationStatus.Error
        };

    private sealed class AuthenticationCallback(
        TaskCompletionSource<BiometricAuthenticationResult> completion) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            completion.TrySetResult(BiometricAuthenticationResult.Succeeded);
        }

        public override void OnAuthenticationError(int errorCode, ICharSequence errString)
        {
            completion.TrySetResult(new BiometricAuthenticationResult(
                MapAuthenticationError(errorCode),
                errString.ToString()));
        }

        public override void OnAuthenticationFailed()
        {
            completion.TrySetResult(new BiometricAuthenticationResult(BiometricAuthenticationStatus.Failed));
        }

        private static BiometricAuthenticationStatus MapAuthenticationError(int errorCode) =>
            errorCode switch
            {
                BiometricPrompt.ErrorCanceled => BiometricAuthenticationStatus.Canceled,
                BiometricPrompt.ErrorNegativeButton => BiometricAuthenticationStatus.Canceled,
                BiometricPrompt.ErrorUserCanceled => BiometricAuthenticationStatus.Canceled,
                BiometricPrompt.ErrorLockout => BiometricAuthenticationStatus.LockedOut,
                BiometricPrompt.ErrorLockoutPermanent => BiometricAuthenticationStatus.LockedOut,
                BiometricPrompt.ErrorNoBiometrics => BiometricAuthenticationStatus.NotEnrolled,
                BiometricPrompt.ErrorHwNotPresent => BiometricAuthenticationStatus.NotAvailable,
                BiometricPrompt.ErrorHwUnavailable => BiometricAuthenticationStatus.NotAvailable,
                _ => BiometricAuthenticationStatus.Error
            };
    }
}
