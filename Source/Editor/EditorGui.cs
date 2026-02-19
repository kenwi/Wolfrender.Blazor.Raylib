using System.Numerics;
using Game.Systems;
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
    private string _savePath = "resources/level.json";
    private string _loadJsonPath = "resources/level.json";
    private string _loadTmxPath = "resources/map1.tmx";
    private string _statusMessage = "";
    private float _statusTimer;

    // Pre-rendered rotated door texture for palette display
    private RenderTexture2D _rotatedDoorTexture;

    // Window visibility toggles
    private bool _showLayers = true;
    private bool _showTilePalette = true;
    private bool _showCursorInfo = true;
    private bool _showEnemyProperties = true;
    private bool _showDebugLog = true;

    public float GuiScale => _guiScale;
    public string StatusMessage => _statusMessage;
    public float StatusTimer => _statusTimer;

    public EditorGui(MapData mapData)
    {
        _mapData = mapData;

        // Pre-render the door texture rotated 90 degrees for the tile palette (vertical door, ID 8)
        if (mapData.Textures.Count > 6)
        {
            var doorTex = mapData.Textures[6];
            _rotatedDoorTexture = LoadRenderTexture(doorTex.Width, doorTex.Height);
            BeginTextureMode(_rotatedDoorTexture);
            ClearBackground(Raylib_cs.Color.Blank);
            DrawTexturePro(
                doorTex,
                new Rectangle(0, 0, doorTex.Width, doorTex.Height),
                new Rectangle(doorTex.Width / 2f, doorTex.Height / 2f, doorTex.Width, doorTex.Height),
                new Vector2(doorTex.Width / 2f, doorTex.Height / 2f),
                90f,
                Raylib_cs.Color.White
            );
            EndTextureMode();
        }
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
        bool isSimulating, EnemySystem enemySystem, DoorSystem doorSystem,
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

            if (ImGui.BeginMenu("Window"))
            {
                if (ImGui.MenuItem("Layers", null, _showLayers))
                    _showLayers = !_showLayers;

                if (ImGui.MenuItem("Tile Palette", null, _showTilePalette))
                    _showTilePalette = !_showTilePalette;

                if (ImGui.MenuItem("Cursor Info", null, _showCursorInfo))
                    _showCursorInfo = !_showCursorInfo;

                if (ImGui.MenuItem("Enemy Properties", null, _showEnemyProperties))
                    _showEnemyProperties = !_showEnemyProperties;

                if (ImGui.MenuItem("Debug Log", null, _showDebugLog))
                    _showDebugLog = !_showDebugLog;

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Simulation"))
            {
                if (ImGui.MenuItem(isSimulating ? "Stop Simulation" : "Start Simulation", "P"))
                {
                    toggleSimulation = true;
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

    public void RenderFileDialogs(Action refreshLayers)
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
                    LevelSerializer.LoadFromJson(_mapData, _loadJsonPath);
                    refreshLayers();
                    _statusMessage = $"Loaded from {_loadJsonPath}";
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
                    LevelSerializer.LoadFromTmx(_mapData, _loadTmxPath);
                    refreshLayers();
                    _statusMessage = $"Loaded TMX from {_loadTmxPath}";
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
    }

    // ─── Layer Panel ─────────────────────────────────────────────────────────────

    public void RenderLayerPanel(List<EditorLayer> layers, ref int activeLayerIndex)
    {
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
                activeLayerIndex = i;
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
        }

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "RMB drag: Pan");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "LMB: Paint tile / Place enemy");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Scroll / +/-: Zoom");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "1-9: Activate layer");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Ctrl+1-9: Toggle visibility");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "C: Toggle cursor follow");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Del: Delete selected enemy");

        ImGui.End();
    }

    // ─── Tile Palette ────────────────────────────────────────────────────────────

    public void RenderTilePalette(List<EditorLayer> layers, int activeLayerIndex, ref uint selectedTileId)
    {
        if (!_showTilePalette) return;

        ImGui.SetNextWindowPos(new Vector2(10, 45), ImGuiCond.FirstUseEver);
        ImGui.Begin("Tile Palette", ref _showTilePalette, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Active Layer: {layers[activeLayerIndex].Name}");
        ImGui.Separator();

        float buttonSize = 64f;

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

        ImGui.Separator();
        ImGui.Text("Tiles:");

        int columns = 3;
        for (int i = 0; i < _mapData.Textures.Count; i++)
        {
            uint tileId = (uint)(i + 1);
            var texture = _mapData.Textures[i];

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

            IntPtr texId;
            if (tileId == 8 && _rotatedDoorTexture.Texture.Id != 0)
            {
                texId = new IntPtr(_rotatedDoorTexture.Texture.Id);
            }
            else
            {
                texId = new IntPtr(texture.Id);
            }

            var uv0 = (tileId == 8) ? new Vector2(0, 1) : new Vector2(0, 0);
            var uv1 = (tileId == 8) ? new Vector2(1, 0) : new Vector2(1, 1);

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

    // ─── Cursor Info Panel ───────────────────────────────────────────────────────

    public void RenderInfoPanel(
        int tileX, int tileY, Vector2 worldPos, bool tileInBounds,
        bool cursorInfoFollowsMouse, List<EditorLayer> layers, string enemiesLayerName)
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
            float wx = worldPos.X * Utilities.LevelData.QuadSize;
            float wy = worldPos.Y * Utilities.LevelData.QuadSize;
            ImGui.Text($"X: {wx:F1}  Z: {wy:F1}");

            ImGui.Spacing();
            ImGui.Text("Tile Contents");
            ImGui.Separator();
            foreach (var layer in layers)
            {
                if (layer.Name == enemiesLayerName)
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

    // ─── Enemy Properties Panel ──────────────────────────────────────────────────

    public void RenderEnemyPropertiesPanel(
        ref int selectedEnemyIndex,
        ref bool isEditingPatrolPath, ref int patrolEditEnemyIndex,
        List<PatrolWaypoint> patrolPathInProgress)
    {
        if (!_showEnemyProperties) return;
        if (selectedEnemyIndex < 0 || selectedEnemyIndex >= _mapData.Enemies.Count)
            return;

        var enemy = _mapData.Enemies[selectedEnemyIndex];

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 500), ImGuiCond.FirstUseEver);
        ImGui.Begin("Enemy Properties", ref _showEnemyProperties, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(_guiScale);

        ImGui.Text($"Enemy #{selectedEnemyIndex}");
        ImGui.Separator();

        int tileX = enemy.TileX;
        int tileY = enemy.TileY;
        if (ImGui.InputInt("Tile X", ref tileX))
        {
            enemy.TileX = Math.Clamp(tileX, 0, _mapData.Width - 1);
        }
        if (ImGui.InputInt("Tile Y", ref tileY))
        {
            enemy.TileY = Math.Clamp(tileY, 0, _mapData.Height - 1);
        }

        ImGui.Spacing();

        float worldX = enemy.TileX * Utilities.LevelData.QuadSize;
        float worldZ = enemy.TileY * Utilities.LevelData.QuadSize;
        ImGui.Text("World Position");
        ImGui.Text($"  X: {worldX:F1}  Y: 2.0  Z: {worldZ:F1}");

        ImGui.Spacing();

        const float step = MathF.PI / 4f;
        int rotIndex = (int)MathF.Round(enemy.Rotation / step);
        rotIndex = Math.Clamp(rotIndex, 0, 7);
        string[] labels = { "0°", "45°", "90°", "135°", "180°", "225°", "270°", "315°" };
        if (ImGui.SliderInt("Rotation", ref rotIndex, 0, 7, labels[rotIndex]))
        {
            enemy.Rotation = rotIndex * step;
        }

        ImGui.Spacing();

        ImGui.Text($"Type: {enemy.EnemyType}");
        if (ImGui.Button("Guard")) enemy.EnemyType = "Guard";

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
                    enemy.PatrolPath.Clear();
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
            _mapData.Enemies.RemoveAt(selectedEnemyIndex);
            selectedEnemyIndex = -1;
        }
        ImGui.PopStyleColor(2);

        ImGui.End();
    }

    // ─── Debug Log Panel ────────────────────────────────────────────────────────

    public void RenderDebugLogPanel()
    {
        if (!_showDebugLog) return;
        Utilities.Debug.RenderLogWindow(_guiScale);
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
