using Transportados.Client.Services.Versioning;

namespace Transportados.Mobile.Services;

public sealed class MauiApplicationVersionProvider : IApplicationVersionProvider
{
    public string DisplayVersion => AppInfo.Current.VersionString;
}
