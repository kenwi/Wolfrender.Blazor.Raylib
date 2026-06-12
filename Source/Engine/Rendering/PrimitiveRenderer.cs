using System.IO;
using System.Linq;
using System.Numerics;
using Game.Features.WorldObjects;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

public static class PrimitiveRenderer
{
    // Color key for transparency: #980088 (R:152, G:0, B:136) - public for hit-testing vs CPU sprite samples.
    public static readonly Color SpriteTransparencyKey = new Color(152, 0, 136, 255);
    private static Shader? _colorKeyShader;
    private static int _colorKeyShaderLoc;

    // Distance-based lighting (walls/floors) and sprite billboards (color key + same falloff)
    private static Shader? _lightingShader;
    private static int _lightingShaderPlayerPosLoc;
    private static int _lightingShaderMaxDistanceLoc;
    private static int _lightingShaderMinBrightnessLoc;
    private static int _lightingShaderTileLightCountLoc;
    private static int _lightingShaderTileLightRadiusLoc;
    private static readonly int[] _lightingShaderTileLightLocs = new int[LightObjectEncoding.MaxShaderLights];

    private static Shader? _spriteLitShader;
    private static int _spriteLitPlayerPosLoc;
    private static int _spriteLitMaxDistanceLoc;
    private static int _spriteLitMinBrightnessLoc;
    private static int _spriteLitColorKeyLoc;
    private static int _spriteLitTileLightCountLoc;
    private static int _spriteLitTileLightRadiusLoc;
    private static readonly int[] _spriteLitTileLightLocs = new int[LightObjectEncoding.MaxShaderLights];

    private static Vector3 _lightingPlayerPosition;
    private static float _cachedMaxLightDistance = 50f;
    private static float _cachedMinBrightness = 0.1f;
    private static float _cachedTileLightRadius = LightObjectEncoding.DefaultRadius;
    private static Vector3[] _activeTileLights = Array.Empty<Vector3>();

    // rlgl texture coordinates use a different V origin than DrawTexturePro.
    // Flip V for world polygon rendering so in-game orientation matches the editor preview.
    private static void TexCoordFlippedY(float u, float v) => Rlgl.TexCoord2f(u, 1.0f - v);
    
    private static void EnsureColorKeyShader()
    {
        if (_colorKeyShader.HasValue) return;
 
        // Load shader (Raylib will use default vertex shader)
        // _colorKeyShader = LoadShaderFromMemory(null, fragmentShader);
        _colorKeyShader = LoadShader(null, Res.Path("resources/shaders/transparency.fs"));
        if (_colorKeyShader.HasValue)
        {
            _colorKeyShaderLoc = GetShaderLocation(_colorKeyShader.Value, "colorKey");
            
            // Set the color key (in 0-255 range for shader)
            float[] colorKeyArray = { SpriteTransparencyKey.R, SpriteTransparencyKey.G, SpriteTransparencyKey.B };
            SetShaderValue(_colorKeyShader.Value, _colorKeyShaderLoc, colorKeyArray, ShaderUniformDataType.Vec3);
        }
    }
    
    private static string ReadShaderFile(string relativePath) =>
        File.ReadAllText(Res.Path(relativePath));

    private static Shader LoadSceneLitShader(string fragmentFileName)
    {
        string vertexSource = ReadShaderFile("resources/shaders/lighting.vs");
        string fragmentSource = ReadShaderFile($"resources/shaders/{fragmentFileName}");
        string commonSource = ReadShaderFile("resources/shaders/lighting_common.glsl");

        int mainIndex = fragmentSource.IndexOf("void main", StringComparison.Ordinal);
        if (mainIndex < 0)
            throw new InvalidOperationException($"Scene fragment shader missing main(): {fragmentFileName}");

        string combinedFragment = fragmentSource[..mainIndex] + commonSource + fragmentSource[mainIndex..];
        return LoadShaderFromMemory(vertexSource, combinedFragment);
    }

    private static void CacheTileLightUniformLocations(
        Shader shader,
        int[] tileLightLocs,
        out int tileLightCountLoc,
        out int tileLightRadiusLoc)
    {
        tileLightCountLoc = GetShaderLocation(shader, "tileLightCount");
        tileLightRadiusLoc = GetShaderLocation(shader, "tileLightRadius");
        for (int i = 0; i < tileLightLocs.Length; i++)
            tileLightLocs[i] = GetShaderLocation(shader, $"tileLight{i}");
    }

    private static void EnsureLightingShader()
    {
        if (_lightingShader.HasValue) return;

        _lightingShader = LoadSceneLitShader("lighting.fs");
        _lightingShaderPlayerPosLoc = GetShaderLocation(_lightingShader.Value, "playerPosition");
        _lightingShaderMaxDistanceLoc = GetShaderLocation(_lightingShader.Value, "maxLightDistance");
        _lightingShaderMinBrightnessLoc = GetShaderLocation(_lightingShader.Value, "minBrightness");
        CacheTileLightUniformLocations(
            _lightingShader.Value,
            _lightingShaderTileLightLocs,
            out _lightingShaderTileLightCountLoc,
            out _lightingShaderTileLightRadiusLoc);
    }

    private static void EnsureSpriteLitShader()
    {
        if (_spriteLitShader.HasValue) return;

        _spriteLitShader = LoadSceneLitShader("sprite_lit.fs");
        _spriteLitPlayerPosLoc = GetShaderLocation(_spriteLitShader.Value, "playerPosition");
        _spriteLitMaxDistanceLoc = GetShaderLocation(_spriteLitShader.Value, "maxLightDistance");
        _spriteLitMinBrightnessLoc = GetShaderLocation(_spriteLitShader.Value, "minBrightness");
        _spriteLitColorKeyLoc = GetShaderLocation(_spriteLitShader.Value, "colorKey");
        CacheTileLightUniformLocations(
            _spriteLitShader.Value,
            _spriteLitTileLightLocs,
            out _spriteLitTileLightCountLoc,
            out _spriteLitTileLightRadiusLoc);

        float[] colorKeyArray = { SpriteTransparencyKey.R, SpriteTransparencyKey.G, SpriteTransparencyKey.B };
        SetShaderValue(_spriteLitShader.Value, _spriteLitColorKeyLoc, colorKeyArray, ShaderUniformDataType.Vec3);
    }

    private static void ApplyLightingUniforms(
        Shader shader,
        int playerPosLoc,
        int maxDistanceLoc,
        int minBrightnessLoc,
        int tileLightCountLoc,
        int[] tileLightLocs,
        int tileLightRadiusLoc)
    {
        float[] playerPosArray = { _lightingPlayerPosition.X, _lightingPlayerPosition.Y, _lightingPlayerPosition.Z };
        SetShaderValue(shader, playerPosLoc, playerPosArray, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, maxDistanceLoc, _cachedMaxLightDistance, ShaderUniformDataType.Float);
        SetShaderValue(shader, minBrightnessLoc, _cachedMinBrightness, ShaderUniformDataType.Float);

        int lightCount = Math.Min(_activeTileLights.Length, LightObjectEncoding.MaxShaderLights);
        if (tileLightCountLoc >= 0)
            SetShaderValue(shader, tileLightCountLoc, (float)lightCount, ShaderUniformDataType.Float);

        for (int i = 0; i < lightCount; i++)
        {
            if (tileLightLocs[i] < 0)
                continue;

            var light = _activeTileLights[i];
            float[] lightPos = { light.X, light.Y, light.Z };
            SetShaderValue(shader, tileLightLocs[i], lightPos, ShaderUniformDataType.Vec3);
        }

        if (tileLightRadiusLoc >= 0)
            SetShaderValue(shader, tileLightRadiusLoc, _cachedTileLightRadius, ShaderUniformDataType.Float);
    }

    /// <summary>Re-upload lighting uniforms to the wall/floor shader (call after <see cref="BeginShaderMode"/>).</summary>
    public static void ApplyWallLightingUniforms()
    {
        if (!_lightingShader.HasValue)
            return;

        ApplyLightingUniforms(
            _lightingShader.Value,
            _lightingShaderPlayerPosLoc,
            _lightingShaderMaxDistanceLoc,
            _lightingShaderMinBrightnessLoc,
            _lightingShaderTileLightCountLoc,
            _lightingShaderTileLightLocs,
            _lightingShaderTileLightRadiusLoc);
    }

    private static float ExponentialFalloffBrightness(float distance, float maxDistance, float minBright)
    {
        if (distance <= 0f || maxDistance <= 0f)
            return 1f;

        float falloffFactor = maxDistance / 3f;
        float expBrightness = MathF.Exp(-distance / falloffFactor);
        float brightness = expBrightness * (1f - minBright) + minBright;
        return Math.Clamp(brightness, minBright, 1f);
    }

    private static float ComputeDistanceBrightness(Vector3 worldPos)
    {
        float brightness = ExponentialFalloffBrightness(
            Vector3.Distance(worldPos, _lightingPlayerPosition),
            _cachedMaxLightDistance,
            _cachedMinBrightness);

        foreach (var lightPos in _activeTileLights)
        {
            float dx = worldPos.X - lightPos.X;
            float dz = worldPos.Z - lightPos.Z;
            float tileDistance = MathF.Sqrt(dx * dx + dz * dz);
            brightness = MathF.Max(
                brightness,
                ExponentialFalloffBrightness(tileDistance, _cachedTileLightRadius, _cachedMinBrightness));
        }

        return brightness;
    }

    private static Color ApplyDistanceLighting(Color color, Vector3 worldPos)
    {
        float brightness = ComputeDistanceBrightness(worldPos);
        return new Color(
            (byte)Math.Clamp(color.R * brightness, 0, 255),
            (byte)Math.Clamp(color.G * brightness, 0, 255),
            (byte)Math.Clamp(color.B * brightness, 0, 255),
            color.A);
    }

    private static void BeginSpriteLitDraw()
    {
        EnsureSpriteLitShader();
        if (!_spriteLitShader.HasValue) return;

        ApplyLightingUniforms(
            _spriteLitShader.Value,
            _spriteLitPlayerPosLoc,
            _spriteLitMaxDistanceLoc,
            _spriteLitMinBrightnessLoc,
            _spriteLitTileLightCountLoc,
            _spriteLitTileLightLocs,
            _spriteLitTileLightRadiusLoc);
        BeginShaderMode(_spriteLitShader.Value);
    }

    private static void EndSpriteLitDraw()
    {
        if (_spriteLitShader.HasValue)
            EndShaderMode();
    }

    public static void SetLightingParameters(
        Vector3 playerPosition,
        float maxDistance = 50.0f,
        float minBrightness = 0.1f,
        ReadOnlySpan<Vector3> tileLights = default,
        float tileLightRadius = LightObjectEncoding.DefaultRadius)
    {
        _lightingPlayerPosition = playerPosition;
        _cachedMaxLightDistance = maxDistance;
        _cachedMinBrightness = minBrightness;
        _cachedTileLightRadius = tileLightRadius;
        _activeTileLights = tileLights.Length == 0
            ? Array.Empty<Vector3>()
            : tileLights.ToArray();

        EnsureLightingShader();
        if (_lightingShader.HasValue)
            ApplyWallLightingUniforms();
    }
    
    public static Shader? GetLightingShader()
    {
        EnsureLightingShader();
        return _lightingShader;
    }

    /// <summary>Draw a screen-space sprite region with the shared magenta color-key shader.</summary>
    public static void DrawScreenSprite(
        Texture2D texture,
        Rectangle source,
        Rectangle dest,
        Color tint)
    {
        EnsureColorKeyShader();
        if (_colorKeyShader.HasValue)
            BeginShaderMode(_colorKeyShader.Value);

        DrawTexturePro(texture, source, dest, Vector2.Zero, 0f, tint);

        if (_colorKeyShader.HasValue)
            EndShaderMode();
    }
    public static void DrawCubeTexture(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color,
        Vector3 playerPosition)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        // Calculate direction from cube center to player (only XZ plane for 2.5D view)
        Vector3 toPlayer = playerPosition - position;
        toPlayer.Y = 0; // Ignore Y component for face culling
        float toPlayerLength = toPlayer.Length();
        
        
        if (toPlayerLength < 0.001f)
        {
            // Player is too close, draw all faces as fallback
            toPlayer = new Vector3(0, 0, 1);
            toPlayerLength = 1.0f;
        }
        
        Vector3 toPlayerNormalized = toPlayer / toPlayerLength;

        // Face normals (pointing outward)
        Vector3 frontNormal = new Vector3(0, 0, 1);   // +Z
        Vector3 backNormal = new Vector3(0, 0, -1);   // -Z
        Vector3 rightNormal = new Vector3(1, 0, 0);   // +X
        Vector3 leftNormal = new Vector3(-1, 0, 0);   // -X

        // Calculate dot products to determine which faces are visible
        // Positive dot = face is facing player
        float frontDot = Vector3.Dot(frontNormal, toPlayerNormalized);
        float backDot = Vector3.Dot(backNormal, toPlayerNormalized);
        float rightDot = Vector3.Dot(rightNormal, toPlayerNormalized);
        float leftDot = Vector3.Dot(leftNormal, toPlayerNormalized);

        // Find the two faces with highest dot products (most visible)
        // We'll draw at most 2 faces
        var faces = new[]
        {
            (dot: frontDot, name: "front"),
            (dot: backDot, name: "back"),
            (dot: rightDot, name: "right"),
            (dot: leftDot, name: "left")
        };

        // Sort by dot product descending and take top 2
        var visibleFaces = faces.OrderByDescending(f => f.dot).Take(2).ToList();
        
        int quadsDrawn = 0;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Draw only visible faces
        foreach (var face in visibleFaces)
        {
            if (face.name == "front" && face.dot > 0)
            {
                // Front Face
                Rlgl.Normal3f(0.0f, 0.0f, 1.0f);
                TexCoordFlippedY(0.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
                TexCoordFlippedY(1.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
                TexCoordFlippedY(1.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
                TexCoordFlippedY(0.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
                quadsDrawn++;
            }
            else if (face.name == "back" && face.dot > 0)
            {
                // Back Face
                Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
                TexCoordFlippedY(1.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
                TexCoordFlippedY(1.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
                TexCoordFlippedY(0.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
                TexCoordFlippedY(0.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
                quadsDrawn++;
            }
            else if (face.name == "right" && face.dot > 0)
            {
                // Right face
                Rlgl.Normal3f(1.0f, 0.0f, 0.0f);
                TexCoordFlippedY(1.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
                TexCoordFlippedY(1.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
                TexCoordFlippedY(0.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
                TexCoordFlippedY(0.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
                quadsDrawn++;
            }
            else if (face.name == "left" && face.dot > 0)
            {
                // Left Face
                Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
                TexCoordFlippedY(0.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
                TexCoordFlippedY(1.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
                TexCoordFlippedY(1.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
                TexCoordFlippedY(0.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
                quadsDrawn++;
            }
        }

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        LevelData.DrawedQuads += quadsDrawn;
    }
    
    public static void DrawCubeTexture(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Front Face
        Rlgl.Normal3f(0.0f, 0.0f, 1.0f);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);

        // Back Face
        Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);

        // // Top Face
        // Rlgl.Normal3f(0.0f, 1.0f, 0.0f);
        // Rlgl.TexCoord2f(0.0f, 1.0f);
        // Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
        // Rlgl.TexCoord2f(0.0f, 0.0f);
        // Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 0.0f);
        // Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 1.0f);
        // Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);

        // // Bottom Face
        // Rlgl.Normal3f(0.0f, -1.0f, 0.0f);
        // Rlgl.TexCoord2f(1.0f, 1.0f);
        // Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        // Rlgl.TexCoord2f(0.0f, 1.0f);
        // Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        // Rlgl.TexCoord2f(0.0f, 0.0f);
        // Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 0.0f);
        // Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);

        // Right face
        Rlgl.Normal3f(1.0f, 0.0f, 0.0f);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        // Left Face
        Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);

        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    public static void DrawFloorTexture(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Top Face
        Rlgl.Normal3f(0.0f, 1.0f, 0.0f);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        LevelData.DrawedQuads += 1;
    }

    public static void DrawCeilingTexture(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Bottom Face
        Rlgl.Normal3f(0.0f, -1.0f, 0.0f);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        LevelData.DrawedQuads += 1;
    }

    public static void DrawDoorTextureH(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z + (0.5f * 4);

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Front Face
        Rlgl.Normal3f(0.0f, 0.0f, 1.0f);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);

        // Back Face
        Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        LevelData.DrawedQuads += 2;
    }

    public static void DrawDoorTextureV(
        Texture2D texture,
        Vector3 position,
        float width,
        float height,
        float length,
        Color color)
    {
        float x = position.X - (0.5f * 4);
        float y = position.Y;
        float z = position.Z;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);


        // Right face
        Rlgl.Normal3f(1.0f, 0.0f, 0.0f);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        // // Left Face
        Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
        TexCoordFlippedY(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        TexCoordFlippedY(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        TexCoordFlippedY(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        TexCoordFlippedY(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);

        // // Front Face
        // Rlgl.Normal3f(0.0f, 0.0f, 1.0f);
        // Rlgl.TexCoord2f(0.0f, 0.0f);
        // Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 0.0f);
        // Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 1.0f);
        // Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        // Rlgl.TexCoord2f(0.0f, 1.0f);
        // Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);

        // // Back Face
        // Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
        // Rlgl.TexCoord2f(1.0f, 0.0f);
        // Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        // Rlgl.TexCoord2f(1.0f, 1.0f);
        // Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        // Rlgl.TexCoord2f(0.0f, 1.0f);
        // Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        // Rlgl.TexCoord2f(0.0f, 0.0f);
        // Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        LevelData.DrawedQuads += 2;
    }

    public static void DrawSpriteTexture(
        Texture2D texture,
        Vector3 position,
        Vector3 cameraPosition,
        Color color,
        float width = 4f,
        float height = 4f,
        float angle = 0f,
        float heightOffset = 0.0f,
        Rectangle? frameRect = null,
        bool quantizeToEightDirections = true,
        Vector3? cameraViewTarget = null,
        SpriteBillboardGeometry.FacingMode facingMode = SpriteBillboardGeometry.FacingMode.PointAtCamera)
    {
        SpriteBillboardGeometry.ComputeBillboardBasis(
            position,
            cameraPosition,
            cameraViewTarget,
            facingMode,
            quantizeToEightDirections,
            out Vector3 directionToCamera,
            out Vector3 right);

        SpriteBillboardGeometry.BuildQuadCorners(
            position, right, width, height, angle,
            out Vector3 topLeft, out Vector3 topRight, out Vector3 bottomRight, out Vector3 bottomLeft);

        // Calculate texture coordinates for frame clipping
        float texLeft, texRight, texTop, texBottom;
        
        if (frameRect.HasValue)
        {
            var frame = frameRect.Value;
            // Normalize texture coordinates (0-1 range) based on frame rectangle
            texLeft = frame.X / texture.Width;
            texRight = (frame.X + frame.Width) / texture.Width;
            texTop = frame.Y / texture.Height;
            texBottom = (frame.Y + frame.Height) / texture.Height;
        }
        else
        {
            // Use full texture if no frame specified
            texLeft = 0.0f;
            texRight = 1.0f;
            texTop = 0.0f;
            texBottom = 1.0f;
        }
        
        BeginSpriteLitDraw();

        // Draw the quad
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Calculate normal (facing camera)
        var normal = directionToCamera;
        Rlgl.Normal3f(normal.X, normal.Y, normal.Z);

        // Top-left (flip X texture coordinate to fix Y-axis flip)
        Rlgl.TexCoord2f(texRight, texTop);
        Rlgl.Vertex3f(topLeft.X, topLeft.Y + heightOffset, topLeft.Z);

        // Top-right
        Rlgl.TexCoord2f(texLeft, texTop);
        Rlgl.Vertex3f(topRight.X, topRight.Y + heightOffset, topRight.Z);

        // Bottom-right
        Rlgl.TexCoord2f(texLeft, texBottom);
        Rlgl.Vertex3f(bottomRight.X, bottomRight.Y + heightOffset, bottomRight.Z);

        // Bottom-left
        Rlgl.TexCoord2f(texRight, texBottom);
        Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y + heightOffset, bottomLeft.Z);

        Rlgl.End();
        Rlgl.SetTexture(0);

        EndSpriteLitDraw();

        LevelData.DrawedQuads += 1;
    }

    /// <summary>
    /// Untextured semi-transparent billboard using the same layout as <see cref="DrawSpriteTexture"/>.
    /// Distance falloff matches wall lighting via CPU tint at the billboard anchor.
    /// </summary>
    /// <param name="position">World position at the center of the billboard quad (same anchor as <see cref="DrawSpriteTexture"/>).</param>
    public static void DrawColoredBillboard(
        Vector3 position,
        Vector3 cameraPosition,
        Color color,
        float width = 4f,
        float height = 4f,
        float angle = 0f,
        bool quantizeToEightDirections = false,
        Vector3? cameraViewTarget = null,
        SpriteBillboardGeometry.FacingMode facingMode = SpriteBillboardGeometry.FacingMode.ViewAligned)
    {
        DrawColoredBillboard(
            position, cameraPosition, color, null, null,
            width, height, angle, quantizeToEightDirections, cameraViewTarget, facingMode);
    }

    /// <summary>
    /// Camera-facing textured billboard from an atlas sub-rectangle (no magenta color-key shader).
    /// </summary>
    public static void DrawTexturedBillboard(
        Vector3 position,
        Vector3 cameraPosition,
        Texture2D texture,
        Rectangle frameRect,
        Color color,
        float width = 4f,
        float height = 4f,
        float angle = 0f,
        bool quantizeToEightDirections = false)
    {
        DrawColoredBillboard(position, cameraPosition, color, texture, frameRect, width, height, angle, quantizeToEightDirections);
    }

    /// <summary>
    /// Camera-facing billboard at <paramref name="position"/> (quad center). When <paramref name="texture"/> and
    /// <paramref name="frameRect"/> are set, draws a sub-rectangle from the atlas without the magenta color-key shader.
    /// </summary>
    public static void DrawColoredBillboard(
        Vector3 position,
        Vector3 cameraPosition,
        Color color,
        Texture2D? texture,
        Rectangle? frameRect,
        float width = 4f,
        float height = 4f,
        float angle = 0f,
        bool quantizeToEightDirections = false,
        Vector3? cameraViewTarget = null,
        SpriteBillboardGeometry.FacingMode facingMode = SpriteBillboardGeometry.FacingMode.ViewAligned)
    {
        SpriteBillboardGeometry.ComputeBillboardBasis(
            position,
            cameraPosition,
            cameraViewTarget,
            facingMode,
            quantizeToEightDirections,
            out Vector3 directionToCamera,
            out Vector3 right);

        SpriteBillboardGeometry.BuildQuadCorners(
            position, right, width, height, angle,
            out Vector3 topLeft, out Vector3 topRight, out Vector3 bottomRight, out Vector3 bottomLeft);

        if (texture is { Id: > 0 } tex && frameRect.HasValue)
        {
            var frame = frameRect.Value;
            float texLeft = frame.X / tex.Width;
            float texRight = (frame.X + frame.Width) / tex.Width;
            float texTop = frame.Y / tex.Height;
            float texBottom = (frame.Y + frame.Height) / tex.Height;

            BeginSpriteLitDraw();

            Rlgl.SetTexture(tex.Id);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.Normal3f(directionToCamera.X, directionToCamera.Y, directionToCamera.Z);

            Rlgl.TexCoord2f(texRight, texTop);
            Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.TexCoord2f(texLeft, texTop);
            Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
            Rlgl.TexCoord2f(texLeft, texBottom);
            Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.TexCoord2f(texRight, texBottom);
            Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);

            Rlgl.End();
            Rlgl.SetTexture(0);

            EndSpriteLitDraw();
        }
        else
        {
            color = ApplyDistanceLighting(color, position);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(color.R, color.G, color.B, color.A);
            Rlgl.Normal3f(directionToCamera.X, directionToCamera.Y, directionToCamera.Z);

            Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
            Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);

            Rlgl.End();
        }

        LevelData.DrawedQuads += 1;
    }
}

