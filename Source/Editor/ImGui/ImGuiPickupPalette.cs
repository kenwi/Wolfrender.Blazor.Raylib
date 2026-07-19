using System.Numerics;
using Game.Features.Pickups;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui pickup palette window + buttons. Mirrors Components/Editor/Pickups.</summary>
public sealed class ImGuiPickupPalette
{
    private Dictionary<PickupType, RenderTexture2D>? _icons;

    public void EnsureIcons(MapData mapData)
    {
        if (_icons != null) return;
        if (mapData.GameTextures.Count <= PickupSprites.ObjectsTextureIndex) return;

        var objectsTex = mapData.GameTextures[PickupSprites.ObjectsTextureIndex];
        if (objectsTex.Id == 0) return;

        _icons = new Dictionary<PickupType, RenderTexture2D>();
        foreach (PickupType type in Enum.GetValues<PickupType>())
        {
            var rt = LoadRenderTexture(PickupSprites.FrameSize, PickupSprites.FrameSize);
            BeginTextureMode(rt);
            ClearBackground(Color.Black);
            var src = PickupSprites.GetFrameRect(type);
            PrimitiveRenderer.DrawScreenSprite(
                objectsTex,
                src,
                new Rectangle(0, 0, PickupSprites.FrameSize, PickupSprites.FrameSize),
                Color.White);
            EndTextureMode();
            _icons[type] = rt;
        }
    }

    public void RenderWindow(EditorState editorState, ref bool showWindow, float guiScale)
    {
        if (!showWindow) return;

        float buttonSize = PickupSprites.PaletteIconSize;
        int columns = PickupSprites.PaletteColumns;
        float gridWidth = columns * buttonSize + (columns - 1) * ImGui.GetStyle().ItemSpacing.X;
        float windowWidth = gridWidth + ImGui.GetStyle().WindowPadding.X * 2f;

        ImGui.SetNextWindowPos(new Vector2(10, 280), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, 0), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pickup Palette", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        ImGui.Text($"Active Layer: {editorState.ActiveLayer.Name}");
        ImGui.Separator();
        ImGui.Text("Pickups:");

        RenderButtons(editorState, buttonSize, columns);

        ImGui.Separator();
        ImGui.Text($"Selected: {editorState.SelectedPickupType}");

        ImGui.End();
    }

    public void RenderButtons(EditorState editorState, float buttonSize, int columns)
    {
        EnsureIcons(editorState.MapData);
        bool hasIcons = _icons is { Count: > 0 };

        int index = 0;
        foreach (PickupType type in Enum.GetValues<PickupType>())
        {
            if (index % columns != 0)
                ImGui.SameLine();

            ImGui.PushID(index + 500);

            bool selected = editorState.SelectedPickupType == type;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            bool picked = false;
            if (hasIcons && _icons!.TryGetValue(type, out var icon))
            {
                var texId = new IntPtr(icon.Texture.Id);
                if (ImGui.ImageButton($"{type}_pickup", texId, new Vector2(buttonSize, buttonSize),
                        new Vector2(0, 1), new Vector2(1, 0)))
                    picked = true;
            }
            else
            {
                var color = PickupVisuals.GetColor(type);
                var imguiColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, 0.85f);
                ImGui.PushStyleColor(ImGuiCol.Button, imguiColor);
                if (ImGui.Button($"{type}", new Vector2(buttonSize, buttonSize)))
                    picked = true;
                ImGui.PopStyleColor();
            }

            if (picked)
            {
                editorState.SelectedPickupType = type;
                editorState.SwitchToPickupLayer();
            }

            if (selected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{type}");

            ImGui.PopID();
            index++;
        }
    }
}
