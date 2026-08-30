using Windows.Storage.Pickers;
using WaveDL.Models;
using WinRT.Interop;

namespace WaveDL.Helpers;

/// <summary>
/// Bridges the few places where a ViewModel needs the main window handle (folder picker)
/// or a window-level action (theme). Wired once by <c>MainWindow</c>.
/// </summary>
public static class UiInterop
{
    public static nint WindowHandle { get; set; }

    public static Action<AppThemeMode>? ThemeApplier { get; set; }

    public static async Task<string?> PickFolderAsync()
    {
        if (WindowHandle == 0)
        {
            return null;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public static void ApplyTheme(AppThemeMode mode) => ThemeApplier?.Invoke(mode);
}
