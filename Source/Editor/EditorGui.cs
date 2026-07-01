using System.Numerics;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>
/// All ImGui panels for the level editor: menu bar, file dialogs, layer panel, tile palette,
/// cursor info, and enemy properties.
/// </summary>
public class EditorGui
{
    private readonly MapData _mapData;

    // GUI scaling
    private float _guiScale = 1.5f;
    private const float MinGuiScale = 0.5f;
    private const float MaxGuiScale = 4.0f;
    private const float GuiScaleStep = 0.25f;

    // File dialog state
    private bool _showSaveDialog;
    private bool _showLoadJsonDialog;
    private bool _showLoadTmxDialog;
    private bool _showLoadBmpDialog;
    private string _savePath = Res.Path("resources/level.json");
    private string _loadJsonPath = Res.Path("resources/level.json");
    private string _loadTmxPath = Res.Path("resources/map1.tmx");
    private string _loadBmpPath = Res.Path("resources/level.bmp");
    private string _statusMessage = "";
    private float _statusTimer;

    // Pickup palette icons (color-keyed on black)
    private Dictionary<PickupType, RenderTexture2D>? _pickupPaletteIcons;

    // Door palette icons (door texture + optional lock badge)
    private Dictionary<uint, RenderTexture2D>? _doorPaletteIcons;

    // Blocking object palette icons (Objects.png upper grid)
    private RenderTexture2D[]? _objectPaletteIcons;

    // Window visibility toggles
    private bool _showLayers = true;
    private bool _showTilePalette = true;
    private bool _showCursorInfo = true;
    private bool _showEnemyProperties = true;
    private bool _showPlayerProperties = true;
    private bool _showPickupPalette = true;
    private bool _showPickupProperties = true;
    private bool _showWallProperties = true;
    private bool _showDebugLog = false;
    private bool _showPathfinding;
    private bool _showSoundPropagation;

    private string? _entityPropertiesActiveWindowTitle;

    public float GuiScale => _guiScale;
    public string StatusMessage => _statusMessage;
    public float StatusTimer => _statusTimer;

    public EditorGui(MapData mapData)
    {
        _mapData = mapData;
    }

    /// <summary>
    /// Handle GUI scaling hotkeys (Ctrl+/-, Ctrl+0).
    /// </summary>
    public void HandleScalingInput()
    {
        bool ctrlHeld = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrlHeld)
        {
            if (IsKeyPressed(KeyboardKey.Equal) || IsKeyPressed(KeyboardKey.KpAdd))
            {
                _guiScale = Math.Clamp(_guiScale + GuiScaleStep, MinGuiScale, MaxGuiScale);
            }
            else if (IsKeyPressed(KeyboardKey.Minus) || IsKeyPressed(KeyboardKey.KpSubtract))
            {
                _guiScale = Math.Clamp(_guiScale - GuiScaleStep, MinGuiScale, MaxGuiScale);
            }
        }
    }

    /// <summary>
    /// Tick the status message timer.
    /// </summary>
    public void UpdateStatusTimer(float deltaTime)
    {
        if (_statusTimer > 0)
            _statusTimer -= deltaTime;
    }

    // ─── Menu Bar ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Render the main menu bar and handle simulation toggle.
    /// Returns true if the simulation toggle was pressed in the menu.
    /// </summary>
    public bool RenderMenuBar(
        bool isSimulating, EditorState? editorState, EnemySystem enemySystem, DoorSystem doorSystem,
        Action clearLevel, Action refreshLayers)
    {
        bool toggleSimulation = false;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 10));

        if (ImGui.BeginMainMenuBar())
        {
            ImGui.SetWindowFontScale(_guiScale);

            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New"))
                {
                    clearLevel();
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Save JSON..."))
                {
                    _showSaveDialog = true;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Load JSON..."))
                {
                    _showLoadJsonDialog = true;
                }

                if (ImGui.MenuItem("Load TMX..."))
                {
                    _showLoadTmxDialog = true;
                }

                if (ImGui.MenuItem("Load BMP..."))
                {
                    _showLoadBmpDialog = true;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("GUI"))
            {
                if (ImGui.MenuItem("Increase Scaling", "Ctrl++"))
                {
                    _guiScale = Math.Clamp(_guiScale + GuiScaleStep, MinGuiScale, MaxGuiScale);
                }

                if (ImGui.MenuItem("Decrease Scaling", "Ctrl+-"))
                {
                    _guiScale = Math.Clamp(_guiScale - GuiScaleStep, MinGuiScale, MaxGuiScale);
                }

                if (ImGui.MenuItem("Reset Scaling"))
                {
                    _guiScale = 1.5f;
                }

                ImGui.Separator();
                ImGui.Text($"Scale: {_guiScale:F2}x");

                ImGui.EndMenu();
            }

            if (editorState != null && ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z", false, editorState.CanUndo))
                    editorState.Undo();

                if (ImGui.MenuItem("Redo", "Ctrl+Y", false, editorState.CanRedo))
                    editorState.Redo();

                ImGui.Separator();

                if (ImGui.MenuItem("Paint Mode", "B", editorState.ToolMode == EditorState.EditorToolMode.Paint))
                    editorState.SetToolMode(EditorState.EditorToolMode.Paint);

                if (ImGui.MenuItem("Select Mode", "V", editorState.ToolMode == EditorState.EditorToolMode.Select))
                    editorState.SetToolMode(EditorState.EditorToolMode.Select);

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Window"))
            {
                if (ImGui.MenuItem("Layers", null, _showLayers))
                    _showLayers = !_showLayers;

                if (ImGui.MenuItem("Tile Palette", null, _showTilePalette))
                    _showTilePalette = !_showTilePalette;

                if (ImGui.MenuItem("Pickup Palette", null, _showPickupPalette))
                    _showPickupPalette = !_showPickupPalette;

                if (ImGui.MenuItem("Cursor Info", null, _showCursorInfo))
                    _showCursorInfo = !_showCursorInfo;

                if (ImGui.MenuItem("Enemy Properties", null, _showEnemyProperties))
                    _showEnemyProperties = !_showEnemyProperties;

                if (ImGui.MenuItem("Player Properties", null, _showPlayerProperties))
                    _showPlayerProperties = !_showPlayerProperties;

                if (ImGui.MenuItem("Pickup Properties", null, _showPickupProperties))
                    _showPickupProperties = !_showPickupProperties;

                if (ImGui.MenuItem("Wall Properties", null, _showWallProperties))
                    _showWallProperties = !_showWallProperties;

                if (ImGui.MenuItem("Debug Log", null, _showDebugLog))
                    _showDebugLog = !_showDebugLog;

                if (ImGui.MenuItem("Pathfinding Visualizer", null, _showPathfinding))
                    _showPathfinding = !_showPathfinding;

                if (ImGui.MenuItem("Sound Propagation", null, _showSoundPropagation))
                    _showSoundPropagation = !_showSoundPropagation;

                if (editorState != null)
                {
                    bool showRooms = editorState.ShowRoomOverlay;
                    if (ImGui.MenuItem("Room Overlay", null, showRooms))
                        editorState.ShowRoomOverlay = !showRooms;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Options"))
            {
                if (editorState != null)
                {
                    bool showPatrolPaths = editorState.ShowPatrolPaths;
                    if (ImGui.MenuItem("Show Patrol Paths", null, showPatrolPaths))
                        editorState.ShowPatrolPaths = !showPatrolPaths;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Simulation"))
            {
                if (ImGui.MenuItem(isSimulating ? "Stop Simulation" : "Start Simulation", "P"))
                {
                    toggleSimulation = true;
                }

                if (editorState != null)
                {
                    ImGui.Separator();

                    bool drawFov = editorState.DrawEnemyLineOfSight;
                    if (ImGui.MenuItem("Enemy Line of Sight", null, drawFov))
                        editorState.DrawEnemyLineOfSight = !drawFov;

                    bool drawPaths = editorState.DrawEnemyPaths;
                    if (ImGui.MenuItem("Enemy Paths", null, drawPaths))
                        editorState.DrawEnemyPaths = !drawPaths;
                }

                ImGui.EndMenu();
            }

            if (isSimulating)
            {
                ImGui.SameLine(ImGui.GetWindowWidth() - 250);
                ImGui.TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), "SIMULATING");
            }

            ImGui.EndMainMenuBar();
        }

        ImGui.PopStyleVar();

        return toggleSimulation;
    }

    // ─── File Dialogs ────────────────────────────────────────────────────────────

    public void RenderFileDialogs(EditorState state)
    {
        if (_showSaveDialog)
        {
            ImGui.OpenPopup("Save Level JSON");
            _showSaveDialog = false;
        }
        if (_showLoadJsonDialog)
        {
            ImGui.OpenPopup("Load Level JSON");
            _showLoadJsonDialog = false;
        }
        if (_showLoadTmxDialog)
        {
            ImGui.OpenPopup("Load Level TMX");
            _showLoadTmxDialog = false;
        }
        if (_showLoadBmpDialog)
        {
            ImGui.OpenPopup("Load Level BMP");
            _showLoadBmpDialog = false;
        }

        // Save dialog
        ImGui.SetNextWindowSize(new Vector2(500, 0));
        if (ImGui.BeginPopupModal("Save Level JSON", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.SetWindowFontScale(_guiScale);
            ImGui.Text("Save level data to JSON file:");
            ImGui.InputText("Path", ref _savePath, 512);

            ImGui.Spacing();
            if (ImGui.Button("Save", new Vector2(120, 0)))
            {
                try
                {
                    LevelSerializer.SaveToJson(_mapData, _savePath);
                    _statusMessage = $"Saved to {_savePath}";
                }
                catch (Exception ex)
                {
                    _statusMessage = $"Error saving: {ex.Message}";
                }
                _statusTimer = 4f;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        // Load JSON dialog
        ImGui.SetNextWindowSize(new Vector2(500, 0));
        if (ImGui.BeginPopupModal("Load Level JSON", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.SetWindowFontScale(_guiScale);
            ImGui.Text("Load level data from JSON file:");
            ImGui.InputText("Path", ref _loadJsonPath, 512);

            ImGui.Spacing();
            if (ImGui.Button("Load", new Vector2(120, 0)))
            {
                try
                {
                    state.LoadLevelFromJson(_loadJsonPath);
                    _statusMessage = state.StatusMessage;
                }
                catch (Exception ex)
                {
                    _statusMessage = $"Error loading: {ex.Message}";
                }
                _statusTimer = 4f;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        // Load TMX dialog
        ImGui.SetNextWindowSize(new Vector2(500, 0));
        if (ImGui.BeginPopupModal("Load Level TMX", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.SetWindowFontScale(_guiScale);
            ImGui.Text("Load level data from TMX file:");
            ImGui.InputText("Path", ref _loadTmxPath, 512);

            ImGui.Spacing();
            if (ImGui.Button("Load", new Vector2(120, 0)))
            {
                try
                {
                    state.LoadLevelFromTmx(_loadTmxPath);
                    _statusMessage = state.StatusMessage;
                }
                catch (Exception ex)
                {
                    _statusMessage = $"Error loading TMX: {ex.Message}";
                }
                _statusTimer = 4f;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        // Load BMP dialog
        ImGui.SetNextWindowSize(new Vector2(500, 0));
        if (ImGui.BeginPopupModal("Load Level BMP", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.SetWindowFontScale(_guiScale);
            ImGui.Text("Load level from BMP image (7x7 pixel tiles, 1px border):");
            ImGui.TextWrapped("Black tiles will be placed as greystone on the floor layer.");
            ImGui.InputText("Path", ref _loadBmpPath, 512);

            ImGui.Spacing();
            if (ImGui.Button("Load", new Vector2(120, 0)))
            {
                try
                {
                    state.LoadLevelFromBmp(_loadBmpPath);
                    _statusMessage = state.StatusMessage;
                }
                catch (Exception ex)
                {
                    _statusMessage = $"Error loading BMP: {ex.Message}";
                }
                _statusTimer = 4f;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    // ─── Layer Panel ─────────────────────────────────────────────────────────────

    public void RenderLayerPanel(List<EditorLayer> layers, EditorState editorState)
    {
        ref int activeLayerIndex = ref editorState.ActiveLayerIndex;
        if (!_showLayers) return;

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 45), ImGuiCond.FirstUseEver);
        ImGui.Begin("Layers", ref _showLayers, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text("Render Order (top = drawn first)");
        ImGui.Text("Click layer name to select for painting");
        ImGui.Separator();

        int? swapFrom = null;
        int? swapTo = null;

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            ImGui.PushID(i);

            bool visible = layer.IsVisible;
            if (ImGui.Checkbox("##visible", ref visible))
            {
                layer.IsVisible = visible;
            }

            ImGui.SameLine();
            bool isActive = i == activeLayerIndex;
            if (isActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.9f, 1f));
            }
            if (ImGui.Button(layer.Name, new Vector2(120, 0)))
            {
                editorState.SetActiveLayerIndex(i);
            }
            if (isActive)
            {
                ImGui.PopStyleColor(2);
            }

            ImGui.SameLine(200);
            if (i > 0 && ImGui.SmallButton("Up"))
            {
                swapFrom = i;
                swapTo = i - 1;
            }
            ImGui.SameLine();
            if (i < layers.Count - 1 && ImGui.SmallButton("Down"))
            {
                swapFrom = i;
                swapTo = i + 1;
            }

            ImGui.PopID();
        }

        if (swapFrom.HasValue && swapTo.HasValue)
        {
            if (activeLayerIndex == swapFrom.Value)
                activeLayerIndex = swapTo.Value;
            else if (activeLayerIndex == swapTo.Value)
                activeLayerIndex = swapFrom.Value;

            (layers[swapFrom.Value], layers[swapTo.Value]) = (layers[swapTo.Value], layers[swapFrom.Value]);
            editorState.SanitizeSelectedTileForActiveLayer();
        }

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "RMB drag: Pan");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "LMB: Paint tile / Place enemy");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "B / V: Paint / Select mode");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Scroll / +/-: Zoom");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "1-9: Activate layer");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Ctrl+1-9: Toggle visibility");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "C: Toggle cursor follow");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Ctrl+Z / Ctrl+Y: Undo / Redo");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Del: Delete selected enemy");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "P: Toggle simulation");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "E: Open door (while simulating)");

        ImGui.End();
    }

    // ─── Tile Palette ────────────────────────────────────────────────────────────

    public void RenderTilePalette(List<EditorLayer> layers, int activeLayerIndex, EditorState editorState, ref uint selectedTileId, ref PickupType selectedPickupType)
    {
        if (!_showTilePalette) return;

        ImGui.SetNextWindowPos(new Vector2(10, 45), ImGuiCond.FirstUseEver);
        ImGui.Begin("Tile Palette", ref _showTilePalette, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Active Layer: {layers[activeLayerIndex].Name}");
        ImGui.Separator();

        string modeLabel = editorState.ToolMode == EditorState.EditorToolMode.Paint ? "Paint" : "Select";
        ImGui.Text($"Tool: {modeLabel}");
        if (ImGui.Button("Paint (B)"))
            editorState.SetToolMode(EditorState.EditorToolMode.Paint);
        ImGui.SameLine();
        if (ImGui.Button("Select (V)"))
            editorState.SetToolMode(EditorState.EditorToolMode.Select);
        ImGui.Separator();

        if (editorState.ToolMode == EditorState.EditorToolMode.Select
            && layers[activeLayerIndex].Name != EditorState.WallsLayerName)
        {
            ImGui.TextWrapped("Select mode applies on the Walls layer. Switch to Walls and click a wall tile.");
            ImGui.End();
            return;
        }

        if (editorState.IsWallSelectMode)
        {
            ImGui.TextWrapped("Click a wall on the map to edit secret properties.");
            ImGui.End();
            return;
        }

        float buttonSize = PickupSprites.PaletteIconSize;

        bool isEraserSelected = selectedTileId == 0;
        if (isEraserSelected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
        }
        if (ImGui.Button("Eraser\n(Empty)", new Vector2(buttonSize + 20, buttonSize)))
        {
            selectedTileId = 0;
        }
        if (isEraserSelected)
        {
            ImGui.PopStyleColor(2);
        }

        if (layers[activeLayerIndex].Name == EditorState.ObjectsLayerName)
        {
            RenderObjectPaletteButtons(editorState, ref selectedTileId, buttonSize);
            ImGui.Separator();
            ImGui.Text($"Selected: {(selectedTileId == 0 ? "Eraser" : $"Object {selectedTileId}")}");
            ImGui.End();
            return;
        }

        if (layers[activeLayerIndex].Name == EditorState.DoorsLayerName)
        {
            RenderDoorPaletteButtons(editorState, ref selectedTileId, buttonSize);
            ImGui.Separator();
            ImGui.Text($"Selected: {(selectedTileId == 0 ? "Eraser" : DoorTileEncoding.GetPaletteLabel(selectedTileId))}");
            ImGui.End();
            return;
        }

        ImGui.Separator();
        ImGui.Text("Tiles:");

        editorState.SanitizeSelectedTileForActiveLayer();

        int columns = TileSpriteSheet.PaletteColumns;
        for (int i = 0; i < TileSpriteSheet.TileCount && i < _mapData.TileTextures.Count; i++)
        {
            uint tileId = (uint)(i + 1);
            if (!EditorTilePalette.IsArchitecturalTileId(tileId))
                continue;

            var texture = _mapData.TileTextures[i];

            if (i % columns != 0)
                ImGui.SameLine();

            ImGui.PushID(i + 100);

            bool isSelected = selectedTileId == tileId;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            var texId = new IntPtr(texture.Id);
            var uv0 = new Vector2(0, 0);
            var uv1 = new Vector2(1, 1);

            if (ImGui.ImageButton($"tile_{i}", texId, new Vector2(buttonSize, buttonSize), uv0, uv1))
            {
                selectedTileId = tileId;
            }

            if (isSelected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Tile ID: {tileId}");
            }

            ImGui.PopID();
        }

        ImGui.Separator();
        ImGui.Text($"Selected: {(selectedTileId == 0 ? "Eraser" : $"ID {selectedTileId}")}");

        ImGui.End();
    }

    private void EnsureDoorPaletteIcons()
    {
        if (_doorPaletteIcons != null) return;
        int size = PickupSprites.PaletteIconSize;
        _doorPaletteIcons = new Dictionary<uint, RenderTexture2D>();
        foreach (var entry in DoorTileEncoding.PaletteEntries)
        {
            if (entry.TextureIndex < 0 || entry.TextureIndex >= _mapData.TileTextures.Count)
                continue;

            var doorTex = _mapData.TileTextures[entry.TextureIndex];
            if (doorTex.Id == 0) continue;

            var rt = LoadRenderTexture(size, size);
            BeginTextureMode(rt);
            ClearBackground(Color.Black);
            DrawDoorPaletteIcon(doorTex, size, entry.Vertical, entry.LockKind);
            EndTextureMode();
            _doorPaletteIcons[entry.Id] = rt;
        }
    }

    private static void DrawDoorPaletteIcon(Texture2D doorTex, int size, bool vertical, DoorLockKind lockKind)
    {
        float half = size / 2f;
        if (vertical)
        {
            DrawTexturePro(
                doorTex,
                new Rectangle(0, 0, doorTex.Width, doorTex.Height),
                new Rectangle(half, half, size, size),
                new Vector2(half, half),
                90f,
                Color.White);
        }
        else
        {
            DrawTexturePro(
                doorTex,
                new Rectangle(0, 0, doorTex.Width, doorTex.Height),
                new Rectangle(0, 0, size, size),
                Vector2.Zero,
                0f,
                Color.White);
        }

        if (lockKind != DoorLockKind.None)
            DrawDoorLockBadge(size, lockKind);
    }

    private static void DrawDoorLockBadge(int tileSize, DoorLockKind lockKind)
    {
        var lockColor = lockKind == DoorLockKind.Gold
            ? new Color(255, 210, 40, 255)
            : new Color(200, 220, 255, 255);
        string label = lockKind == DoorLockKind.Gold ? "G" : "S";
        float cx = tileSize * 0.75f;
        float cy = tileSize * 0.25f;
        float radius = tileSize * 0.2f;
        DrawCircle((int)cx, (int)cy, radius, lockColor);
        int fontSize = (int)(tileSize * 0.35f);
        DrawText(label, (int)(tileSize * 0.65f), (int)(tileSize * 0.12f), fontSize, Color.Black);
    }

    private void EnsureObjectPaletteIcons()
    {
        if (_objectPaletteIcons != null) return;
        if (_mapData.GameTextures.Count <= PickupSprites.ObjectsTextureIndex) return;

        var objectsTex = _mapData.GameTextures[PickupSprites.ObjectsTextureIndex];
        if (objectsTex.Id == 0) return;

        int size = ObjectSprites.PaletteIconSize;
        _objectPaletteIcons = new RenderTexture2D[ObjectSprites.ObjectCount];
        for (int i = 0; i < ObjectSprites.ObjectCount; i++)
        {
            var rt = LoadRenderTexture(size, size);
            BeginTextureMode(rt);
            ClearBackground(Color.Black);
            PrimitiveRenderer.DrawScreenSprite(
                objectsTex,
                ObjectSprites.GetFrameRect(i),
                new Rectangle(0, 0, size, size),
                Color.White);
            EndTextureMode();
            _objectPaletteIcons[i] = rt;
        }
    }

    private void RenderObjectPaletteButtons(EditorState editorState, ref uint selectedTileId, float buttonSize)
    {
        EnsureObjectPaletteIcons();
        bool hasIcons = _objectPaletteIcons != null;
        int columns = ObjectSprites.PaletteColumns;

        for (int i = 0; i < ObjectSprites.ObjectCount; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            uint objectId = (uint)(i + 1);
            ImGui.PushID(i + 700);

            bool selected = selectedTileId == objectId;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            bool picked = false;
            if (hasIcons && _objectPaletteIcons![i].Texture.Id != 0)
            {
                var texId = new IntPtr(_objectPaletteIcons[i].Texture.Id);
                if (ImGui.ImageButton($"object_{objectId}", texId, new Vector2(buttonSize, buttonSize),
                        new Vector2(0, 1), new Vector2(1, 0)))
                    picked = true;
            }
            else if (ImGui.Button($"Obj {objectId}", new Vector2(buttonSize, buttonSize)))
            {
                picked = true;
            }

            if (picked)
            {
                selectedTileId = objectId;
                editorState.SwitchToObjectLayer();
            }

            if (selected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Object ID: {objectId}");

            ImGui.PopID();
        }
    }

    private void RenderDoorPaletteButtons(EditorState editorState, ref uint selectedTileId, float buttonSize)
    {
        EnsureDoorPaletteIcons();
        bool hasIcons = _doorPaletteIcons is { Count: > 0 };
        int columns = DoorTileEncoding.PaletteColumns;

        int index = 0;
        foreach (var entry in DoorTileEncoding.PaletteEntries)
        {
            if (index % columns != 0)
                ImGui.SameLine();

            ImGui.PushID(index + 600);

            bool selected = selectedTileId == entry.Id;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            bool picked = false;
            if (hasIcons && _doorPaletteIcons!.TryGetValue(entry.Id, out var icon))
            {
                var texId = new IntPtr(icon.Texture.Id);
                if (ImGui.ImageButton($"{entry.Id}_door", texId, new Vector2(buttonSize, buttonSize),
                        new Vector2(0, 1), new Vector2(1, 0)))
                    picked = true;
            }
            else if (ImGui.Button($"{entry.ShortLabel}", new Vector2(buttonSize, buttonSize)))
            {
                picked = true;
            }

            if (picked)
            {
                selectedTileId = entry.Id;
                editorState.SwitchToDoorLayer();
            }

            if (selected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{entry.ShortLabel}\n{entry.Description}\nTile ID: {entry.Id}");

            ImGui.PopID();
            index++;
        }
    }

    public void RenderPickupPalette(EditorState editorState)
    {
        if (!_showPickupPalette) return;

        float buttonSize = PickupSprites.PaletteIconSize;
        int columns = PickupSprites.PaletteColumns;
        float gridWidth = columns * buttonSize + (columns - 1) * ImGui.GetStyle().ItemSpacing.X;
        float windowWidth = gridWidth + ImGui.GetStyle().WindowPadding.X * 2f;

        ImGui.SetNextWindowPos(new Vector2(10, 280), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, 0), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pickup Palette", ref _showPickupPalette, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Active Layer: {editorState.ActiveLayer.Name}");
        ImGui.Separator();
        ImGui.Text("Pickups:");

        RenderPickupPaletteButtons(editorState, buttonSize, columns);

        ImGui.Separator();
        ImGui.Text($"Selected: {editorState.SelectedPickupType}");

        ImGui.End();
    }

    // ─── Cursor Info Panel ───────────────────────────────────────────────────────

    private void EnsurePickupPaletteIcons()
    {
        if (_pickupPaletteIcons != null) return;
        if (_mapData.GameTextures.Count <= PickupSprites.ObjectsTextureIndex) return;

        var objectsTex = _mapData.GameTextures[PickupSprites.ObjectsTextureIndex];
        if (objectsTex.Id == 0) return;

        _pickupPaletteIcons = new Dictionary<PickupType, RenderTexture2D>();
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
            _pickupPaletteIcons[type] = rt;
        }
    }

    private void RenderPickupPaletteButtons(EditorState editorState, float buttonSize, int columns)
    {
        EnsurePickupPaletteIcons();
        bool hasIcons = _pickupPaletteIcons is { Count: > 0 };

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
            if (hasIcons && _pickupPaletteIcons!.TryGetValue(type, out var icon))
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

    public void RenderInfoPanel(
        int tileX, int tileY, Vector2 worldPos, bool tileInBounds,
        bool cursorInfoFollowsMouse, List<EditorLayer> layers)
    {
        if (!_showCursorInfo) return;

        if (cursorInfoFollowsMouse)
        {
            var mouse = GetMousePosition();
            float panelX = mouse.X + 25f;
            float panelY = mouse.Y - 10f;
            ImGui.SetNextWindowPos(new Vector2(panelX, panelY));
        }
        else
        {
            ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 350), ImGuiCond.FirstUseEver);
        }

        var flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        if (cursorInfoFollowsMouse)
        {
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar;
        }

        ImGui.Begin("Cursor Info", ref _showCursorInfo, flags);
        ImGui.SetWindowFontScale(_guiScale);

        if (tileInBounds)
        {
            ImGui.Text("Tile Coordinate");
            ImGui.Separator();
            ImGui.Text($"X: {tileX}  Y: {tileY}");

            ImGui.Spacing();
            ImGui.Text("World Coordinate");
            ImGui.Separator();
            float wx = worldPos.X * LevelData.QuadSize;
            float wy = worldPos.Y * LevelData.QuadSize;
            ImGui.Text($"X: {wx:F1}  Z: {wy:F1}");

            ImGui.Spacing();
            ImGui.Text("Tile Contents");
            ImGui.Separator();
            foreach (var layer in layers)
            {
                if (layer.Name == EditorState.EnemiesLayerName)
                {
                    var enemyHere = _mapData.Enemies.FindIndex(e => e.TileX == tileX && e.TileY == tileY);
                    string status = enemyHere >= 0
                        ? $"{_mapData.Enemies[enemyHere].EnemyType} (#{enemyHere})"
                        : "empty";
                    var color = enemyHere >= 0
                        ? new Vector4(1f, 0.3f, 0.3f, 1f)
                        : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                    ImGui.TextColored(color, $"  {layer.Name}: {status}");
                }
                else if (layer.Name == EditorState.PickupsLayerName)
                {
                    int pickupHere = _mapData.Pickups.FindIndex(p => p.TileX == tileX && p.TileY == tileY);
                    string status = pickupHere >= 0
                        ? $"{_mapData.Pickups[pickupHere].Type} (#{pickupHere})"
                        : "empty";
                    var color = pickupHere >= 0
                        ? new Vector4(0.3f, 1f, 0.5f, 1f)
                        : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                    ImGui.TextColored(color, $"  {layer.Name}: {status}");
                }
                else
                {
                    uint tileId = _mapData.GetTile(layer.Tiles, tileX, tileY);
                    string status = tileId > 0 ? $"ID {tileId}" : "empty";
                    var color = tileId > 0
                        ? new Vector4(0.3f, 1f, 0.3f, 1f)
                        : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                    ImGui.TextColored(color, $"  {layer.Name}: {status}");
                }
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Outside map bounds");
        }

        ImGui.End();
    }

    // ─── Pickup Properties Panel ───────────────────────────────────────────────────

    public void RenderPickupPropertiesPanel(EditorState state, ref int selectedPickupIndex)
    {
        if (!_showPickupProperties) return;
        if (selectedPickupIndex < 0 || selectedPickupIndex >= _mapData.Pickups.Count)
            return;

        var pickup = _mapData.Pickups[selectedPickupIndex];

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 620), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pickup Properties", ref _showPickupProperties, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

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
        {
            state.DeletePickupAt(selectedPickupIndex);
            selectedPickupIndex = -1;
        }

        ImGui.End();
    }

    // ─── Wall Properties Panel ─────────────────────────────────────────────────────

    public void RenderWallPropertiesPanel(EditorState state)
    {
        if (!state.ShouldShowWallPropertiesPanel(_showWallProperties))
            return;

        uint wallTileId = state.GetWallTileAt(state.SelectedWallTileX, state.SelectedWallTileY);
        var secret = state.GetSelectedSecretPlacement();
        bool isSecret = secret != null;
        SecretWallDirection direction = secret?.Direction ?? SecretWallDirection.North;
        int travelTiles = secret?.TravelTiles ?? 1;

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Wall Properties", ref _showWallProperties, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Wall ({state.SelectedWallTileX}, {state.SelectedWallTileY})");
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
                    travelTiles = state.ClampSecretTravelTiles(
                        state.SelectedWallTileX, state.SelectedWallTileY, direction, travelTiles);
                    state.SetWallSecret(true, direction, travelTiles);
                }
            }

            int maxTravel = state.GetMaxSecretTravelTiles(
                state.SelectedWallTileX, state.SelectedWallTileY, direction);
            if (ImGui.InputInt("Travel tiles", ref travelTiles))
            {
                travelTiles = state.ClampSecretTravelTiles(
                    state.SelectedWallTileX, state.SelectedWallTileY, direction, travelTiles);
                state.SetWallSecret(true, direction, travelTiles);
            }

            ImGui.TextDisabled($"Max in direction: {maxTravel}");
        }

        ImGui.End();
    }

    public void RenderEntityPropertiesPanel(
        EditorState state,
        ref int selectedEnemyIndex,
        ref bool isEditingPatrolPath, ref int patrolEditEnemyIndex,
        List<PatrolWaypoint> patrolPathInProgress)
    {
        bool showPlayer = state.ShouldShowPlayerPropertiesPanel(_showPlayerProperties);
        bool showEnemy = state.ShouldShowEnemyPropertiesPanel(_showEnemyProperties);

        if (!showPlayer && !showEnemy)
        {
            _entityPropertiesActiveWindowTitle = null;
            return;
        }

        if (showPlayer)
            RenderPlayerPropertiesPanel(state);
        else
            RenderEnemyPropertiesPanel(state, ref selectedEnemyIndex, ref isEditingPatrolPath,
                ref patrolEditEnemyIndex, patrolPathInProgress);
    }

    private void BeginSyncedEntityPropertiesWindow(string title, ref bool open, EditorState state)
    {
        if (_entityPropertiesActiveWindowTitle != title)
            ImGui.SetNextWindowPos(state.GetEntityPropertiesImGuiPos(GetScreenWidth()), ImGuiCond.Appearing);
        _entityPropertiesActiveWindowTitle = title;
        ImGui.Begin(title, ref open, ImGuiWindowFlags.AlwaysAutoResize);
    }

    private void EndSyncedEntityPropertiesWindow(EditorState state)
    {
        state.SetEntityPropertiesFromImGuiPos(ImGui.GetWindowPos(), GetScreenWidth());
        ImGui.End();
    }

    // ─── Player Properties Panel ─────────────────────────────────────────────────

    private void RenderPlayerPropertiesPanel(EditorState state)
    {
        BeginSyncedEntityPropertiesWindow("Player Properties", ref _showPlayerProperties, state);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text("Player Spawn");
        ImGui.Separator();

        int tileX = state.MapData.Spawn.TileX;
        int tileY = state.MapData.Spawn.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
        {
            tileX = Math.Clamp(tileX, 0, _mapData.Width - 1);
            state.SyncPlayerToSpawnTile(tileX, state.MapData.Spawn.TileY);
        }
        if (ImGui.InputInt("Tile Y", ref tileY))
        {
            tileY = Math.Clamp(tileY, 0, _mapData.Height - 1);
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

        EndSyncedEntityPropertiesWindow(state);
    }

    // ─── Enemy Properties Panel ──────────────────────────────────────────────────

    private void RenderEnemyPropertiesPanel(
        EditorState state,
        ref int selectedEnemyIndex,
        ref bool isEditingPatrolPath, ref int patrolEditEnemyIndex,
        List<PatrolWaypoint> patrolPathInProgress)
    {
        var enemy = _mapData.Enemies[selectedEnemyIndex];

        BeginSyncedEntityPropertiesWindow("Enemy Properties", ref _showEnemyProperties, state);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Enemy #{selectedEnemyIndex}");
        ImGui.Separator();

        int tileX = enemy.TileX;
        int tileY = enemy.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
        {
            state.SetEnemyTilePosition(selectedEnemyIndex, tileX, enemy.TileY);
        }
        if (ImGui.InputInt("Tile Y", ref tileY))
        {
            state.SetEnemyTilePosition(selectedEnemyIndex, enemy.TileX, tileY);
        }

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
        {
            state.SetEnemyRotation(selectedEnemyIndex, rotIndex * step);
        }

        ImGui.Spacing();

        ImGui.Text($"Type: {enemy.EnemyType}");
        if (ImGui.Button("Guard")) state.SetEnemyType(selectedEnemyIndex, "Guard");

        ImGui.Spacing();

        bool startsAsCorpse = enemy.StartsAsCorpse;
        if (ImGui.Checkbox("Corpse (dead on spawn)", ref startsAsCorpse))
            state.SetEnemyStartsAsCorpse(selectedEnemyIndex, startsAsCorpse);

        bool dropsAmmo = enemy.DropsAmmo;
        if (ImGui.Checkbox("Drops ammo on death", ref dropsAmmo))
            state.SetEnemyDropsAmmo(selectedEnemyIndex, dropsAmmo);

        ImGui.Spacing();
        ImGui.Separator();

        // Patrol path
        ImGui.Text("Patrol Path");

        bool showPath = enemy.ShowPatrolPath;
        if (ImGui.Checkbox("Show Path", ref showPath))
        {
            enemy.ShowPatrolPath = showPath;
        }

        if (isEditingPatrolPath && patrolEditEnemyIndex == selectedEnemyIndex)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f),
                $"Editing... ({patrolPathInProgress.Count} waypoints)");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "LMB: Add waypoint");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Enter: Confirm path");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Escape: Cancel");

            if (ImGui.Button("Cancel Editing"))
            {
                isEditingPatrolPath = false;
                patrolPathInProgress.Clear();
                patrolEditEnemyIndex = -1;
            }
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
                {
                    state.ClearEnemyPatrolPath(selectedEnemyIndex);
                }
                ImGui.SameLine();
            }

            if (ImGui.Button("Add Path"))
            {
                isEditingPatrolPath = true;
                patrolEditEnemyIndex = selectedEnemyIndex;
                patrolPathInProgress.Clear();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
        if (ImGui.Button("Delete Enemy", new Vector2(-1, 0)))
        {
            state.DeleteEnemyAt(selectedEnemyIndex);
            selectedEnemyIndex = -1;
        }
        ImGui.PopStyleColor(2);

        EndSyncedEntityPropertiesWindow(state);
    }

    // ─── Debug Log Panel ────────────────────────────────────────────────────────

    public void RenderDebugLogPanel()
    {
        if (!_showDebugLog) return;
        Debug.RenderLogWindow(_guiScale);
    }

    // ─── Pathfinding Visualizer Panel ───────────────────────────────────────────

    /// <summary>
    /// Tool window for visualizing A* paths in the editor: pick a start and end tile,
    /// and the path is recomputed and drawn over the map.
    /// </summary>
    public void RenderPathfindingPanel(EditorState state)
    {
        if (!_showPathfinding) return;

        ImGui.SetNextWindowPos(new Vector2(10, GetScreenHeight() - 280), ImGuiCond.FirstUseEver);
        ImGui.Begin("Pathfinding Visualizer", ref _showPathfinding, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        switch (state.PathPickingMode)
        {
            case EditorState.PathPickMode.Start:
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                    "Click a tile to set START  (Esc: cancel)");
                break;
            case EditorState.PathPickMode.End:
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
            point: state.PathStart,
            assignedColor: new Vector4(0.3f, 1f, 0.4f, 1f),
            buttonLabel: "Pick Start",
            onClick: state.StartPickingPathStart);

        DrawEndpointRow(
            label: "End",
            point: state.PathEnd,
            assignedColor: new Vector4(1f, 0.4f, 0.4f, 1f),
            buttonLabel: "Pick End",
            onClick: state.StartPickingPathEnd);

        ImGui.Separator();

        if (state.PathResult != null && state.PathResult.Count > 0)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f),
                $"Path: {state.PathResult.Count} tiles");
        }
        else if (state.PathStart.HasValue && state.PathEnd.HasValue)
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
            state.RecomputePath();
        ImGui.SameLine();
        if (ImGui.Button("Clear", new Vector2(120, 0)))
            state.ClearPath();

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

    // ─── Sound Propagation Panel ──────────────────────────────────────────────────

    /// <summary>
    /// Tool window for testing tile-based sound propagation in the editor.
    /// </summary>
    public void RenderSoundPropagationPanel(EditorState state)
    {
        if (!_showSoundPropagation) return;

        ImGui.SetNextWindowPos(new Vector2(10, GetScreenHeight() - 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Sound Propagation", ref _showSoundPropagation, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        if (state.SoundPropagationPicking)
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

        if (state.SoundPropagationTiles is { Count: > 0 })
        {
            ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f),
                $"Reached {state.SoundPropagationTiles.Count} tiles");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f),
                "Pick an origin tile to test propagation");
        }

        ImGui.Spacing();

        if (ImGui.Button("Test at tile", new Vector2(120, 0)))
            state.StartSoundPropagationPick();
        ImGui.SameLine();
        if (ImGui.Button("Clear", new Vector2(120, 0)))
            state.ClearSoundPropagation();

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

    /// <summary>
    /// Draw the status message bar at the top center of the screen.
    /// </summary>
    public void DrawStatusMessage()
    {
        if (_statusTimer > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            const int fontSize = 30;
            int textWidth = MeasureText(_statusMessage, fontSize);
            int x = (GetScreenWidth() - textWidth) / 2;
            var statusColor = _statusMessage.StartsWith("Error") ? Raylib_cs.Color.Red : Raylib_cs.Color.Green;
            DrawText(_statusMessage, x, 55, fontSize, statusColor);
        }
    }

    /// <summary>
    /// Set a status message that displays for a duration.
    /// </summary>
    public void SetStatus(string message, float duration = 4f)
    {
        _statusMessage = message;
        _statusTimer = duration;
    }
}
