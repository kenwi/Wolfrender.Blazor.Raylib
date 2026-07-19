using System.Numerics;
using Game.Features.Pickups;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui pickup properties panel. Mirrors Components/Editor/Pickups.</summary>
public sealed class ImGuiPickupProperties
{
    public void Render(EditorState state, ref bool showWindow, float guiScale)
    {
        if (!showWindow) return;
        int selectedPickupIndex = state.SelectedPickupIndex;
        if (selectedPickupIndex < 0 || selectedPickupIndex >= state.MapData.Pickups.Count)
            return;

        var pickup = state.MapData.Pickups[selectedPickupIndex];

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 620), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pickup Properties", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        ImGui.Text($"Pickup #{selectedPickupIndex}");
        ImGui.Separator();

        int tileX = pickup.TileX;
        int tileY = pickup.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
            state.SetPickupTilePosition(selectedPickupIndex, tileX, pickup.TileY);
        if (ImGui.InputInt("Tile Y", ref tileY))
            state.SetPickupTilePosition(selectedPickupIndex, pickup.TileX, tileY);

        int amount = pickup.Amount;
        if (ImGui.InputInt("Amount (0=default)", ref amount))
            state.SetPickupAmount(selectedPickupIndex, amount);

        ImGui.Spacing();
        ImGui.Text("Type:");
        foreach (PickupType type in Enum.GetValues<PickupType>())
        {
            if (ImGui.RadioButton(type.ToString(), pickup.Type == type))
                state.SetPickupType(selectedPickupIndex, type);
        }

        if (ImGui.Button("Delete Pickup"))
            state.DeletePickupAt(selectedPickupIndex);

        ImGui.End();
    }
}
