using Transportados.Client.Models.Auth;

namespace Transportados.Client.Services.Auth;

/// <summary>
/// Platform-agnostic interface for notifying authentication state changes.
/// </summary>
public interface IAuthStateNotifier
{
    void NotifyUserAuthentication(AuthenticatedContext userInfo);

    void NotifyUserLogout();
}
