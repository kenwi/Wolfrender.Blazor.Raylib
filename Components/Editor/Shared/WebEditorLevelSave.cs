using Game.Core.Level;
using Game.Editor;
using Microsoft.JSInterop;

namespace Wolfrender.Blazor.Raylib.Components.Editor;

public static class WebEditorLevelSave
{
    public static async Task SaveAsync(IJSRuntime js, EditorState state, string filename)
    {
        var json = LevelSerializer.SerializeToJson(state.MapData);
        await DownloadFileAsync(js, filename, json);
        state.SetLevelFilename(filename);
        state.SetStatus($"Saved {filename}");
    }

    public static Task QuickSaveAsync(IJSRuntime js, EditorState state) =>
        SaveAsync(js, state, state.LevelFilename);

    private static async Task DownloadFileAsync(IJSRuntime js, string filename, string content)
    {
        await js.InvokeVoidAsync("eval", $@"
            (function() {{
                var blob = new Blob([{System.Text.Json.JsonSerializer.Serialize(content)}], {{ type: 'application/json' }});
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = '{filename}';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }})();
        ");
    }
}
