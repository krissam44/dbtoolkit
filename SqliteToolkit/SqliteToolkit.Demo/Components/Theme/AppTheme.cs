using MudBlazor;

namespace SqliteToolkit.Demo.Theme;

public static class AppTheme
{
    public static MudTheme Dark = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#90CAF9",
            Secondary = "#CE93D8",
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "#1E1E1E",
            DrawerBackground = "#1E1E1E",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#B0B0B0",
            ActionDefault = "#FFFFFF",
            Divider = "#383838"
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
