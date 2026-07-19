using System.Numerics;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui sound-reach visualizer panel.</summary>
public sealed class ImGuiSoundPropagationPanel
{
    public void Render(EditorState state, ref bool showWindow, float guiScale)
    {
        if (!showWindow) return;

        ImGui.SetNextWindowPos(new Vector2(10, GetScreenHeight() - 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Sound Propagation", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        var soundTool = state.SoundPropagationTool;
        if (soundTool.IsPicking)
        {
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                "Click a tile to test propagation  (Esc: cancel)");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Ready");
        }

        ImGui.Separator();

        if (state.IsSimulating)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.5f, 1f), "Using live door states");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "All doors treated as closed");
        }

        ImGui.Separator();

        if (soundTool.OverlayTiles is { Count: > 0 })
        {
            ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f),
                $"Reached {soundTool.OverlayTiles.Count} tiles");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f),
                "Pick an origin tile to test propagation");
        }

        ImGui.Spacing();

        if (ImGui.Button("Test at tile", new Vector2(120, 0)))
            soundTool.StartPick();
        ImGui.SameLine();
        if (ImGui.Button("Clear", new Vector2(120, 0)))
            soundTool.Clear();

        ImGui.End();
    }
}
