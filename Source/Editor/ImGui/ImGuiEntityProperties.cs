using System.Numerics;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>
/// Desktop ImGui player/enemy properties panels with shared screen position.
/// Mirrors Components/Editor/Player and Components/Editor/Enemies.
/// </summary>
public sealed class ImGuiEntityProperties
{
    private string? _activeWindowTitle;

    public void Render(
        EditorState state,
        ref bool showPlayerWindow,
        ref bool showEnemyWindow,
        float guiScale)
    {
        bool showPlayer = state.ShouldShowPlayerPropertiesPanel(showPlayerWindow);
        bool showEnemy = state.ShouldShowEnemyPropertiesPanel(showEnemyWindow);

        if (!showPlayer && !showEnemy)
        {
            _activeWindowTitle = null;
            return;
        }

        if (showPlayer)
            RenderPlayer(state, ref showPlayerWindow, guiScale);
        else
            RenderEnemy(state, ref showEnemyWindow, guiScale);
    }

    private void BeginSyncedWindow(string title, ref bool open, EditorState state)
    {
        if (_activeWindowTitle != title)
            ImGui.SetNextWindowPos(state.GetEntityPropertiesImGuiPos(GetScreenWidth()), ImGuiCond.Appearing);
        _activeWindowTitle = title;
        ImGui.Begin(title, ref open, ImGuiWindowFlags.AlwaysAutoResize);
    }

    private static void EndSyncedWindow(EditorState state)
    {
        state.SetEntityPropertiesFromImGuiPos(ImGui.GetWindowPos(), GetScreenWidth());
        ImGui.End();
    }

    private void RenderPlayer(EditorState state, ref bool showWindow, float guiScale)
    {
        BeginSyncedWindow("Player Properties", ref showWindow, state);
        ImGui.SetWindowFontScale(guiScale);

        ImGui.Text("Player Spawn");
        ImGui.Separator();

        int tileX = state.MapData.Spawn.TileX;
        int tileY = state.MapData.Spawn.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
        {
            tileX = Math.Clamp(tileX, 0, state.MapData.Width - 1);
            state.SyncPlayerToSpawnTile(tileX, state.MapData.Spawn.TileY);
        }
        if (ImGui.InputInt("Tile Y", ref tileY))
        {
            tileY = Math.Clamp(tileY, 0, state.MapData.Height - 1);
            state.SyncPlayerToSpawnTile(state.MapData.Spawn.TileX, tileY);
        }

        ImGui.Spacing();

        float worldX = state.MapData.Spawn.TileX * LevelData.QuadSize;
        float worldZ = state.MapData.Spawn.TileY * LevelData.QuadSize;
        ImGui.Text("World Position");
        ImGui.Text($"  X: {worldX:F1}  Y: {state.MapData.Spawn.WorldY:F1}  Z: {worldZ:F1}");

        ImGui.Spacing();

        int rotIndex = EditorState.GetSpawnRotationIndex(state.MapData.Spawn.Rotation);
        string[] labels = { "0°", "45°", "90°", "135°", "180°", "225°", "270°", "315°" };
        if (ImGui.SliderInt("Rotation", ref rotIndex, 0, 7, labels[rotIndex]))
            state.SetPlayerSpawnRotationIndex(rotIndex);

        EndSyncedWindow(state);
    }

    private void RenderEnemy(EditorState state, ref bool showWindow, float guiScale)
    {
        int selectedEnemyIndex = state.SelectedEnemyIndex;
        var enemy = state.MapData.Enemies[selectedEnemyIndex];

        BeginSyncedWindow("Enemy Properties", ref showWindow, state);
        ImGui.SetWindowFontScale(guiScale);

        ImGui.Text($"Enemy #{selectedEnemyIndex}");
        ImGui.Separator();

        int tileX = enemy.TileX;
        int tileY = enemy.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
            state.SetEnemyTilePosition(selectedEnemyIndex, tileX, enemy.TileY);
        if (ImGui.InputInt("Tile Y", ref tileY))
            state.SetEnemyTilePosition(selectedEnemyIndex, enemy.TileX, tileY);

        ImGui.Spacing();

        float worldX = enemy.TileX * LevelData.QuadSize;
        float worldZ = enemy.TileY * LevelData.QuadSize;
        ImGui.Text("World Position");
        ImGui.Text($"  X: {worldX:F1}  Y: 2.0  Z: {worldZ:F1}");

        ImGui.Spacing();

        const float step = MathF.PI / 4f;
        int rotIndex = (int)MathF.Round(enemy.Rotation / step);
        rotIndex = Math.Clamp(rotIndex, 0, 7);
        string[] labels = { "0°", "45°", "90°", "135°", "180°", "225°", "270°", "315°" };
        if (ImGui.SliderInt("Rotation", ref rotIndex, 0, 7, labels[rotIndex]))
            state.SetEnemyRotation(selectedEnemyIndex, rotIndex * step);

        ImGui.Spacing();

        ImGui.Text($"Type: {enemy.EnemyType}");
        if (ImGui.Button("Guard")) state.SetEnemyType(selectedEnemyIndex, "Guard");
        ImGui.SameLine();
        if (ImGui.Button("Dog")) state.SetEnemyType(selectedEnemyIndex, "Dog");

        ImGui.Spacing();

        bool startsAsCorpse = enemy.StartsAsCorpse;
        if (ImGui.Checkbox("Corpse (dead on spawn)", ref startsAsCorpse))
            state.SetEnemyStartsAsCorpse(selectedEnemyIndex, startsAsCorpse);

        bool dropsAmmo = enemy.DropsAmmo;
        if (ImGui.Checkbox("Drops ammo on death", ref dropsAmmo))
            state.SetEnemyDropsAmmo(selectedEnemyIndex, dropsAmmo);

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.Text("Patrol Path");

        bool showPath = enemy.ShowPatrolPath;
        if (ImGui.Checkbox("Show Path", ref showPath))
            enemy.ShowPatrolPath = showPath;

        if (state.IsEditingPatrolPath && state.PatrolEditEnemyIndex == selectedEnemyIndex)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f),
                $"Editing... ({state.PatrolPathInProgress.Count} waypoints)");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "LMB: Add waypoint");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Enter: Confirm path");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Escape: Cancel");

            if (ImGui.Button("Cancel Editing"))
                state.CancelPatrolPath();
        }
        else
        {
            if (enemy.PatrolPath.Count > 0)
            {
                ImGui.Text($"{enemy.PatrolPath.Count} waypoints");
                for (int w = 0; w < enemy.PatrolPath.Count; w++)
                {
                    var wp = enemy.PatrolPath[w];
                    ImGui.TextColored(new Vector4(0, 0.8f, 1f, 1f),
                        $"  {w + 1}: ({wp.TileX}, {wp.TileY})");
                }

                if (ImGui.Button("Clear Path"))
                    state.ClearEnemyPatrolPath(selectedEnemyIndex);
                ImGui.SameLine();
            }

            if (ImGui.Button("Add Path"))
                state.StartEditingPatrolPath();
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
        if (ImGui.Button("Delete Enemy", new Vector2(-1, 0)))
            state.DeleteEnemyAt(selectedEnemyIndex);
        ImGui.PopStyleColor(2);

        EndSyncedWindow(state);
    }
}
