using MudBlazor;

namespace Transportados.Client.Styling;

public static class TransportadosTheme
{
    public const string LogoGreen = "#00CE6B";
    public const string LogoGreenDark = "#006B4F";
    public const string LogoGreenHover = "#00A957";
    public const string LogoGreenDeep = "#062F23";
    public const string PrimaryContrastText = "#FFFFFF";
    public const string Background = "#F8FAFC";
    public const string BackgroundGray = "#EEF3FB";
    public const string Surface = "#FFFFFF";
    public const string TextPrimary = "#1E293B";
    public const string Divider = "#D6D5D5";
    public const string Error = "#E50000";
    public const string Success = "#26B050";

    public static MudTheme Application { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = LogoGreen,
            PrimaryContrastText = PrimaryContrastText,
            PrimaryDarken = LogoGreenHover,
            Secondary = LogoGreenDark,
            Background = Background,
            BackgroundGray = BackgroundGray,
            Surface = Surface,
            TextPrimary = TextPrimary,
            Divider = Divider,
            DrawerBackground = LogoGreenDark,
            DrawerText = BackgroundGray,
            AppbarBackground = Surface,
            AppbarText = TextPrimary,
            Error = Error,
            Success = Success
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
