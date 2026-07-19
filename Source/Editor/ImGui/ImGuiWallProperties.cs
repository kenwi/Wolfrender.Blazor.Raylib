using System.Numerics;
using Game.Features.LevelProgress;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui wall / secret-wall properties panel. Mirrors web wall editing.</summary>
public sealed class ImGuiWallProperties
{
    public void Render(EditorState state, ref bool showWindow, float guiScale)
    {
        if (!state.ShouldShowWallPropertiesPanel(showWindow))
            return;

        int wallX = state.SecretWallTool.SelectedTileX;
        int wallY = state.SecretWallTool.SelectedTileY;
        uint wallTileId = state.GetWallTileAt(wallX, wallY);
        var secret = state.GetSelectedSecretPlacement();
        bool isSecret = secret != null;
        SecretWallDirection direction = secret?.Direction ?? SecretWallDirection.North;
        int travelTiles = secret?.TravelTiles ?? 1;

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Wall Properties", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        ImGui.Text($"Wall ({wallX}, {wallY})");
        ImGui.Text($"Sprite ID: {wallTileId}");
        ImGui.Separator();

        if (ImGui.Checkbox("Secret wall", ref isSecret))
        {
            state.SetWallSecret(isSecret, direction, travelTiles);
            secret = state.GetSelectedSecretPlacement();
            isSecret = secret != null;
            direction = secret?.Direction ?? SecretWallDirection.North;
            travelTiles = secret?.TravelTiles ?? 1;
        }

        if (isSecret)
        {
            ImGui.Spacing();
            ImGui.Text("Travel direction:");
            foreach (SecretWallDirection value in Enum.GetValues<SecretWallDirection>())
            {
                if (ImGui.RadioButton(value.ToString(), direction == value))
                {
                    direction = value;
                    travelTiles = state.ClampSecretTravelTiles(wallX, wallY, direction, travelTiles);
                    state.SetWallSecret(true, direction, travelTiles);
                }
            }

            int maxTravel = state.GetMaxSecretTravelTiles(wallX, wallY, direction);
            if (ImGui.InputInt("Travel tiles", ref travelTiles))
            {
                travelTiles = state.ClampSecretTravelTiles(wallX, wallY, direction, travelTiles);
                state.SetWallSecret(true, direction, travelTiles);
            }

            ImGui.TextDisabled($"Max in direction: {maxTravel}");
        }

        ImGui.End();
    }
}
