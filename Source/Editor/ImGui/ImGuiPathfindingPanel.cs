using System.Numerics;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui A* path visualizer panel.</summary>
public sealed class ImGuiPathfindingPanel
{
    public void Render(EditorState state, ref bool showWindow, float guiScale)
    {
        if (!showWindow) return;

        ImGui.SetNextWindowPos(new Vector2(10, GetScreenHeight() - 280), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pathfinding Visualizer", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        var pathTool = state.PathfindingTool;
        switch (pathTool.PickingMode)
        {
            case PathfindingEditorTool.PathPickMode.Start:
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                    "Click a tile to set START  (Esc: cancel)");
                break;
            case PathfindingEditorTool.PathPickMode.End:
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                    "Click a tile to set END  (Esc: cancel)");
                break;
            default:
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Ready");
                break;
        }

        ImGui.Separator();

        DrawEndpointRow(
            label: "Start",
            point: pathTool.PathStart,
            assignedColor: new Vector4(0.3f, 1f, 0.4f, 1f),
            buttonLabel: "Pick Start",
            onClick: pathTool.StartPickingStart);

        DrawEndpointRow(
            label: "End",
            point: pathTool.PathEnd,
            assignedColor: new Vector4(1f, 0.4f, 0.4f, 1f),
            buttonLabel: "Pick End",
            onClick: pathTool.StartPickingEnd);

        ImGui.Separator();

        if (pathTool.PathResult != null && pathTool.PathResult.Count > 0)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f),
                $"Path: {pathTool.PathResult.Count} tiles");
        }
        else if (pathTool.PathStart.HasValue && pathTool.PathEnd.HasValue)
        {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "No path found");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f),
                "Pick both endpoints to compute a path");
        }

        ImGui.Spacing();

        if (ImGui.Button("Recompute", new Vector2(120, 0)))
            pathTool.Recompute();
        ImGui.SameLine();
        if (ImGui.Button("Clear", new Vector2(120, 0)))
            pathTool.Clear();

        ImGui.Separator();

        ImGui.Text("While simulating");
        ImGui.Separator();

        bool drawEnemyPaths = state.DrawEnemyPaths;
        if (ImGui.Checkbox("Draw paths for enemies", ref drawEnemyPaths))
            state.DrawEnemyPaths = drawEnemyPaths;

        bool drawEnemyFov = state.DrawEnemyLineOfSight;
        if (ImGui.Checkbox("Draw enemy line of sight", ref drawEnemyFov))
            state.DrawEnemyLineOfSight = drawEnemyFov;

        ImGui.End();
    }

    private static void DrawEndpointRow(
        string label, Vector2? point, Vector4 assignedColor,
        string buttonLabel, Action onClick)
    {
        if (point.HasValue)
        {
            ImGui.TextColored(assignedColor,
                $"{label}: ({(int)point.Value.X}, {(int)point.Value.Y})");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"{label}: not set");
        }

        if (ImGui.Button(buttonLabel, new Vector2(120, 0)))
            onClick();
    }
}
