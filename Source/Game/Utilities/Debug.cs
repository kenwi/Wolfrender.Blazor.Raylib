namespace Game.Utilities;
using System.Numerics;
using rlImGui_cs;
using Raylib_cs;
using Game.Entities;
using Game.Systems;
using ImGuiNET;
using static Raylib_cs.Raylib;

public static class Debug
{
    private static List<Door> _doors = null!;
    private static Player _player = null!;
    private static AnimationSystem _animationSystem = null!;
    private static EnemySystem _enemySystem = null!;

    // ─── Log ────────────────────────────────────────────────────────────────────
    private const int MaxLogEntries = 256;
    private static readonly List<string> _logEntries = new();
    private static bool _logAutoScroll = true;

    /// <summary>
    /// Append a message to the debug log. Safe to call from anywhere (gameplay, simulation, systems, etc.).
    /// </summary>
    public static void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _logEntries.Add(entry);
        if (_logEntries.Count > MaxLogEntries)
            _logEntries.RemoveAt(0);
    }

    /// <summary>
    /// Clear all log entries.
    /// </summary>
    public static void ClearLog() => _logEntries.Clear();

    public static void Setup(List<Door> doors, Player player, AnimationSystem animationSystem, EnemySystem enemySystem)
    {
        if (doors == null || player == null)
            throw new Exception();
        
        _doors = doors;
        _player = player;
        _animationSystem = animationSystem;
        _enemySystem = enemySystem;
        
        // cimgui native library is not available in browser WASM
        if (!OperatingSystem.IsBrowser())
            rlImGui.Setup(true);
    }

    /// <summary>
    /// Draw 3D debug geometry (direction indicators, etc.).
    /// Must be called inside BeginMode3D/EndMode3D, after EndShaderMode so lines are unlit.
    /// </summary>
    public static void Draw3DOverlays(bool isDebugEnabled)
    {
        if (!isDebugEnabled || _enemySystem?.Enemies == null)
            return;

        foreach (var enemy in _enemySystem.Enemies)
        {
            // Direction indicator: line from enemy position along their facing direction
            float lineLength = 3.0f;
            var forwardDir = new Vector3(
                MathF.Cos(enemy.Rotation) * lineLength,
                0,
                MathF.Sin(enemy.Rotation) * lineLength);
            var floorY = enemy.Position.Y - 2.0f + 0.05f;
            var lineStart = new Vector3(enemy.Position.X, floorY, enemy.Position.Z);
            var lineEnd = lineStart + forwardDir;

            DrawLine3D(lineStart, lineEnd, new Raylib_cs.Color(0, 255, 0, 255));
            DrawSphere(lineEnd, 0.15f, new Raylib_cs.Color(0, 255, 0, 255));
        }
    }

    /// <summary>
    /// Draw screen-space debug overlays for entities in the 3D world.
    /// Must be called after compositing the scene to the screen (between BeginDrawing/EndDrawing).
    /// </summary>
    public static void DrawWorldOverlays(bool isDebugEnabled, Camera3D camera, int renderWidth, int renderHeight)
    {
        if (!isDebugEnabled || _enemySystem?.Enemies == null)
            return;

        int screenW = GetScreenWidth();
        int screenH = GetScreenHeight();
        float scaleX = (float)screenW / renderWidth;
        float scaleY = (float)screenH / renderHeight;

        foreach (var enemy in _enemySystem.Enemies)
        {
            if (enemy.EnemyState != EnemyState.COLLIDING)
                continue;

            // Project the enemy's position to the render texture's screen space
            var screenPos = GetWorldToScreenEx(enemy.Position, camera, renderWidth, renderHeight);
            float sx = screenPos.X * scaleX;
            float sy = screenPos.Y * scaleY;

            if (sx >= 0 && sx < screenW && sy >= 0 && sy < screenH)
            {
                const string text = "COLLIDING";
                const int fontSize = 24;
                int textW = MeasureText(text, fontSize);
                DrawText(text, (int)(sx - textW / 2f), (int)(sy - fontSize), fontSize,
                    new Raylib_cs.Color(255, 40, 40, 255));
            }
        }
    }

    public static void Draw(bool isDebugEnabled)
    {
        if (!isDebugEnabled || OperatingSystem.IsBrowser())
            return;
        
        rlImGui.Begin();
        
        // Main debug window
        ImGui.Begin("Debug Info", ImGuiWindowFlags.AlwaysAutoResize);
        
        // Increase font size for better readability
        ImGui.SetWindowFontScale(1.5f);
        
        // Player Information Section
        if (ImGui.CollapsingHeader("Player", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_player != null)
            {
                ImGui.Text("Position:");
                ImGui.SameLine();
                var playerPosition = _player.Position / 4;
                ImGui.Text($"X: {playerPosition.X:F2}, Y: {playerPosition.Y:F2}, Z: {playerPosition.Z:F2}");
                
                // ImGui.Text("Old Position:");
                // ImGui.SameLine();
                // ImGui.Text($"X: {Player.OldPosition.X:F2}, Y: {Player.OldPosition.Y:F2}, Z: {Player.OldPosition.Z:F2}");
                
                ImGui.Text("Velocity:");
                ImGui.SameLine();
                ImGui.Text($"X: {_player.Velocity.X:F2}, Y: {_player.Velocity.Y:F2}, Z: {_player.Velocity.Z:F2}");
                
                ImGui.Separator();
                
                ImGui.Text("Collision Radius:");
                ImGui.SameLine();
                ImGui.Text($"{_player.CollisionRadius:F2}");
                
                ImGui.Text("Move Speed:");
                ImGui.SameLine();
                ImGui.Text($"{_player.MoveSpeed:F2}");
                
                ImGui.Separator();
                
                if (_player.Camera.Position.X != 0 || _player.Camera.Position.Y != 0 || _player.Camera.Position.Z != 0)
                {
                    ImGui.Text("Camera Position:");
                    ImGui.SameLine();
                    ImGui.Text($"X: {_player.Camera.Position.X:F2}, Y: {_player.Camera.Position.Y:F2}, Z: {_player.Camera.Position.Z:F2}");
                    
                    ImGui.Text("Camera Target:");
                    ImGui.SameLine();
                    ImGui.Text($"X: {_player.Camera.Target.X:F2}, Y: {_player.Camera.Target.Y:F2}, Z: {_player.Camera.Target.Z:F2}");
                    
                    ImGui.Text("Camera FOV:");
                    ImGui.SameLine();
                    ImGui.Text($"{_player.Camera.FovY:F2}°");
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Player is null");
            }
        }
        
        // Rendering Information Section
        if (ImGui.CollapsingHeader("Rendering", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text("Drawn Quads:");
            ImGui.SameLine();
            ImGui.Text($"{LevelData.DrawedQuads}");
            
            ImGui.Text($"Resolution: Width: {RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier} Height: {RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier}");
        }
        
        // Enemy System Information Section
        if (ImGui.CollapsingHeader($"Enemy System ({_enemySystem?.Enemies?.Count ?? 0})", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_enemySystem != null)
            {
                if (_enemySystem.Enemies != null && _enemySystem.Enemies.Count > 0)
                {
                    float fontScale = 2;
                    if (ImGui.BeginTable("EnemiesTable", 11, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 40 * fontScale);
                        ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthFixed, 150 * fontScale);
                        ImGui.TableSetupColumn("Rotation", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                        ImGui.TableSetupColumn("Move Speed", ImGuiTableColumnFlags.WidthFixed, 100 * fontScale);
                        ImGui.TableSetupColumn("Frame Rect", ImGuiTableColumnFlags.WidthFixed, 150 * fontScale);
                        ImGui.TableSetupColumn("Frame Col", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                        ImGui.TableSetupColumn("Frame Row", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                        ImGui.TableSetupColumn("Anim Timer", ImGuiTableColumnFlags.WidthFixed, 100 * fontScale);
                        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                        ImGui.TableSetupColumn("Angle To Player", ImGuiTableColumnFlags.WidthFixed, 120 * fontScale);
                        ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 100 * fontScale);
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();
                        
                        for (int i = 0; i < _enemySystem.Enemies.Count; i++)
                        {
                            var enemy = _enemySystem.Enemies[i];
                            ImGui.TableNextRow();
                            
                            // ID
                            ImGui.TableNextColumn();
                            ImGui.Text($"{i}");
                            
                            // Position
                            ImGui.TableNextColumn();
                            var enemyPos = enemy.Position / 4;
                            ImGui.Text($"X: {enemyPos.X:F2}, Y: {enemyPos.Y:F2}, Z: {enemyPos.Z:F2}");
                            
                            // Rotation
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.Rotation:F2} rad ({enemy.Rotation * 180.0 / Math.PI:F2}°)");
                            
                            // Move Speed
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.MoveSpeed:F2}");
                            
                            // Frame Rect
                            ImGui.TableNextColumn();
                            var enemyFrame = enemy.FrameRect;
                            ImGui.Text($"X: {enemyFrame.X:F0}, Y: {enemyFrame.Y:F0}, W: {enemyFrame.Width:F0}, H: {enemyFrame.Height:F0}");
                            
                            // Frame Column Index
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.FrameColumnIndex}");
                            
                            // Frame Row Index
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.FrameRowIndex}");
                            
                            // Animation Timer
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.AnimationTimer:F3}s");
                            
                            // State (with color coding)
                            ImGui.TableNextColumn();
                            var stateColor = enemy.EnemyState switch
                            {
                                EnemyState.IDLE => new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), // Gray
                                EnemyState.WALKING => new System.Numerics.Vector4(0.3f, 1, 0.3f, 1), // Green
                                EnemyState.NOTICING => new System.Numerics.Vector4(1, 1, 0.3f, 1), // Yellow
                                EnemyState.FLEEING => new System.Numerics.Vector4(0.3f, 0.3f, 1, 1), // Blue
                                EnemyState.ATTACKING => new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), // Red
                                EnemyState.DYING => new System.Numerics.Vector4(0.7f, 0.3f, 0.7f, 1), // Purple
                                EnemyState.COLLIDING => new System.Numerics.Vector4(1, 0, 0, 1), // Red
                                _ => new System.Numerics.Vector4(1, 1, 1, 1) // White
                            };
                            ImGui.TextColored(stateColor, enemy.EnemyState.ToString());
                            
                            // Angle To Player
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.AngleToPlayer:F3} rad ({enemy.AngleToPlayer * 180.0 / Math.PI:F2}°)");
                            
                            // Distance From Player
                            ImGui.TableNextColumn();
                            ImGui.Text($"{enemy.DistanceFromPlayer:F2}");
                        }
                        
                        ImGui.EndTable();
                    }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No enemies available");
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Enemy System is null");
            }
        }
        
        // Doors Information Section
        if (ImGui.CollapsingHeader($"Doors ({_doors?.Count ?? 0})", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_doors != null && _doors.Count > 0)
            {
                // Table header
                // Calculate scaled column widths based on font scale
                float fontScale = 2;//ImGui.GetWindowFontScale();
                if (ImGui.BeginTable("DoorsTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 40 * fontScale);
                    ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                    ImGui.TableSetupColumn("Rotation", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                    ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthFixed, 120 * fontScale);
                    ImGui.TableSetupColumn("Start Pos", ImGuiTableColumnFlags.WidthFixed, 120 * fontScale);
                    ImGui.TableSetupColumn("Time Open", ImGuiTableColumnFlags.WidthFixed, 80 * fontScale);
                    ImGui.TableSetupColumn("Time Opening", ImGuiTableColumnFlags.WidthFixed, 90 * fontScale);
                    ImGui.TableSetupScrollFreeze(0, 1); // Make row 0 always visible
                    ImGui.TableHeadersRow();
                    
                    for (int i = 0; i < _doors.Count; i++)
                    {
                        var door = _doors[i];
                        ImGui.TableNextRow();
                        
                        // ID
                        ImGui.TableNextColumn();
                        ImGui.Text($"{i}");
                        
                        // State (with color coding)
                        ImGui.TableNextColumn();
                        var stateColor = door.DoorState switch
                        {
                            DoorState.CLOSED => new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), // Red
                            DoorState.OPENING => new System.Numerics.Vector4(1, 1, 0.3f, 1), // Yellow
                            DoorState.OPEN => new System.Numerics.Vector4(0.3f, 1, 0.3f, 1), // Green
                            DoorState.CLOSING => new System.Numerics.Vector4(1, 0.7f, 0.3f, 1), // Orange
                            _ => new System.Numerics.Vector4(1, 1, 1, 1) // White
                        };
                        ImGui.TextColored(stateColor, door.DoorState.ToString());
                        
                        // Rotation
                        ImGui.TableNextColumn();
                        ImGui.Text(door.DoorRotation.ToString());
                        
                        // Position
                        ImGui.TableNextColumn();
                        ImGui.Text($"({door.Position.X:F2}, {door.Position.Y:F2})");
                        
                        // Start Position
                        ImGui.TableNextColumn();
                        ImGui.Text($"({door.StartPosition.X:F2}, {door.StartPosition.Y:F2})");
                        
                        // Time Open
                        ImGui.TableNextColumn();
                        ImGui.Text($"{door.TimeDoorHasBeenOpen:F2}s");
                        
                        // Time Opening
                        ImGui.TableNextColumn();
                        ImGui.Text($"{door.TimeDoorHasBeenOpening:F2}s");
                    }
                    
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "No doors available");
            }
        }
        
        // Log Section (embedded in the debug info window)
        if (ImGui.CollapsingHeader($"Log ({_logEntries.Count})", ImGuiTreeNodeFlags.DefaultOpen))
        {
            RenderLogContents();
        }

        ImGui.End();
        
        rlImGui.End();
    }

    /// <summary>
    /// Render the debug log as a standalone ImGui window.
    /// Call this between rlImGui.Begin() / rlImGui.End() in any scene (e.g. the level editor).
    /// </summary>
    public static void RenderLogWindow(float guiScale = 1.5f)
    {
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(600, 350), ImGuiCond.FirstUseEver);
        ImGui.Begin($"Debug Log ({_logEntries.Count})###DebugLog");
        ImGui.SetWindowFontScale(guiScale);
        RenderLogContents();
        ImGui.End();
    }

    /// <summary>
    /// Shared log content renderer — used by both the in-game debug panel and the editor log window.
    /// Must be called inside an active ImGui window.
    /// </summary>
    private static void RenderLogContents()
    {
        if (ImGui.Button("Clear"))
            _logEntries.Clear();
        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref _logAutoScroll);

        float logHeight = 300f;
        if (ImGui.BeginChild("LogScrollRegion", new System.Numerics.Vector2(0, logHeight), ImGuiChildFlags.Borders))
        {
            foreach (var entry in _logEntries)
            {
                ImGui.TextUnformatted(entry);
            }

            if (_logAutoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 10f)
                ImGui.SetScrollHereY(1.0f);
        }
        ImGui.EndChild();
    }
}