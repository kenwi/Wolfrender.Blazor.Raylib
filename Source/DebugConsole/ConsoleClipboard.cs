using Raylib_cs;

namespace Game.DebugConsole;

/// <summary>Thin wrapper around Raylib clipboard APIs shared by desktop and WASM.</summary>
internal static class ConsoleClipboard
{
    public static void SetText(string text) => Raylib.SetClipboardText(text ?? string.Empty);

    public static string GetText() => Raylib.GetClipboardText_() ?? string.Empty;
}
