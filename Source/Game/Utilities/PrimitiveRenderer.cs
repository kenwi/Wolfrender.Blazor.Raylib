using System.Linq;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Utilities;

public static class PrimitiveRenderer
{
    // Color key for transparency: #980088 (R:152, G:0, B:136)
    private static readonly Color ColorKey = new Color(152, 0, 136, 255);
    private static Shader? _colorKeyShader;
    private static int _colorKeyShaderLoc;
    
    // Distance-based lighting shader
    private static Shader? _lightingShader;
    private static int _lightingShaderPlayerPosLoc;
    private static int _lightingShaderMaxDistanceLoc;
    private static int _lightingShaderMinBrightnessLoc;
    private static float _maxLightDistance = 1.0f; // Maximum distance for full brightness
    private static float _minBrightness = 0.0f; // Minimum brightness at max distance
    
    private static void EnsureColorKeyShader()
    {
        if (_colorKeyShader.HasValue) return;
 
        // Load shader (Raylib will use default vertex shader)
        // _colorKeyShader = LoadShaderFromMemory(null, fragmentShader);
        _colorKeyShader = LoadShader(null, "resources/shaders/transparency.fs");
        if (_colorKeyShader.HasValue)
        {
            _colorKeyShaderLoc = GetShaderLocation(_colorKeyShader.Value, "colorKey");
            
            // Set the color key (in 0-255 range for shader)
            float[] colorKeyArray = { ColorKey.R, ColorKey.G, ColorKey.B };
            SetShaderValue(_colorKeyShader.Value, _colorKeyShaderLoc, colorKeyArray, ShaderUniformDataType.Vec3);
        }
    }
    
    private static void EnsureLightingShader()
    {
        if (_lightingShader.HasValue) return;

        _lightingShader = LoadShader("resources/shaders/lighting.vs", "resources/shaders/lighting.fs");
        // if (_lightingShader.HasValue)
        {
            _lightingShaderPlayerPosLoc = GetShaderLocation(_lightingShader.Value, "playerPosition");
            _lightingShaderMaxDistanceLoc = GetShaderLocation(_lightingShader.Value, "maxLightDistance");
            _lightingShaderMinBrightnessLoc = GetShaderLocation(_lightingShader.Value, "minBrightness");
            
            // Set default values
            SetShaderValue(_lightingShader.Value, _lightingShaderMaxDistanceLoc, _maxLightDistance, ShaderUniformDataType.Float);
            SetShaderValue(_lightingShader.Value, _lightingShaderMinBrightnessLoc, _minBrightness, ShaderUniformDataType.Float);
        }
    }
    
    public static void SetLightingParameters(Vector3 playerPosition, float maxDistance = 50.0f, float minBrightness = 0.1f)
    {
        EnsureLightingShader();
        if (_lightingShader.HasValue)
        {
            float[] playerPosArray = { playerPosition.X, playerPosition.Y, playerPosition.Z };
            SetShaderValue(_lightingShader.Value, _lightingShaderPlayerPosLoc, playerPosArray, ShaderUniformDataType.Vec3);
            SetShaderValue(_lightingShader.Value, _lightingShaderMaxDistanceLoc, maxDistance, ShaderUniformDataType.Float);
            SetShaderValue(_lightingShader.Value, _lightingShaderMinBrightnessLoc, minBrightness, ShaderUniformDataType.Float);
        }
    }
    
    public static Shader? GetLightingShader()
    {
        EnsureLightingShader();
        return _lightingShader;
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
                Rlgl.TexCoord2f(0.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
                Rlgl.TexCoord2f(1.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
                Rlgl.TexCoord2f(1.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
                Rlgl.TexCoord2f(0.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
                quadsDrawn++;
            }
            else if (face.name == "back" && face.dot > 0)
            {
                // Back Face
                Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
                Rlgl.TexCoord2f(1.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
                Rlgl.TexCoord2f(1.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
                Rlgl.TexCoord2f(0.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
                Rlgl.TexCoord2f(0.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
                quadsDrawn++;
            }
            else if (face.name == "right" && face.dot > 0)
            {
                // Right face
                Rlgl.Normal3f(1.0f, 0.0f, 0.0f);
                Rlgl.TexCoord2f(1.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
                Rlgl.TexCoord2f(1.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
                Rlgl.TexCoord2f(0.0f, 1.0f);
                Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
                Rlgl.TexCoord2f(0.0f, 0.0f);
                Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
                quadsDrawn++;
            }
            else if (face.name == "left" && face.dot > 0)
            {
                // Left Face
                Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
                Rlgl.TexCoord2f(0.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
                Rlgl.TexCoord2f(1.0f, 0.0f);
                Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
                Rlgl.TexCoord2f(1.0f, 1.0f);
                Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
                Rlgl.TexCoord2f(0.0f, 1.0f);
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
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);

        // Back Face
        Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
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
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        // Left Face
        Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
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
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
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
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
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
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);

        // Back Face
        Rlgl.Normal3f(0.0f, 0.0f, -1.0f);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x - width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x - width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
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
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z - length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);

        // // Left Face
        Rlgl.Normal3f(-1.0f, 0.0f, 0.0f);
        Rlgl.TexCoord2f(0.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z - length / 2);
        Rlgl.TexCoord2f(1.0f, 0.0f);
        Rlgl.Vertex3f(x + width / 2, y - height / 2, z + length / 2);
        Rlgl.TexCoord2f(1.0f, 1.0f);
        Rlgl.Vertex3f(x + width / 2, y + height / 2, z + length / 2);
        Rlgl.TexCoord2f(0.0f, 1.0f);
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
        Rectangle? frameRect = null)
    {
        // Calculate direction from sprite to camera (for billboard effect)
        var directionToCamera = cameraPosition - position;
        directionToCamera.Y = 0; // Keep sprite vertical (only rotate around Y-axis)
        
        // Normalize the direction
        var dirLength = directionToCamera.Length();
        if (dirLength < 0.001f)
        {
            directionToCamera = new Vector3(0, 0, 1); // Default forward if too close
        }
        else
        {
            directionToCamera = directionToCamera / dirLength;
        }
        
        // Quantize direction to 8 discrete angles (0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°)
        // Calculate angle in XZ plane (0° = +Z, 90° = +X)
        float angleRad = MathF.Atan2(directionToCamera.X, directionToCamera.Z);
        
        // Normalize angle to [0, 2π)
        if (angleRad < 0) angleRad += 2 * MathF.PI;
        
        // Quantize to nearest of 8 directions (45° = π/4 intervals)
        float quantizedAngleRad = MathF.Round(angleRad / (MathF.PI / 4.0f)) * (MathF.PI / 4.0f);
        
        // Reconstruct direction vector from quantized angle
        directionToCamera = new Vector3(
            MathF.Sin(quantizedAngleRad),
            0,
            MathF.Cos(quantizedAngleRad)
        );

        // Calculate right and up vectors for the billboard
        var right = Vector3.Cross(directionToCamera, Vector3.UnitY);
        var rightLength = right.Length();
        if (rightLength > 0.001f)
        {
            right = right / rightLength;
        }
        else
        {
            right = Vector3.UnitX; // Fallback
        }

        var up = Vector3.UnitY;

        // Apply rotation around the up vector (Y-axis)
        var cosAngle = MathF.Cos(angle);
        var sinAngle = MathF.Sin(angle);
        
        // Rotate the right vector around the up vector
        var rotatedRight = new Vector3(
            right.X * cosAngle - right.Z * sinAngle,
            right.Y,
            right.X * sinAngle + right.Z * cosAngle
        );

        // Calculate the four corners of the sprite quad
        var halfWidth = rotatedRight * (width / 2);
        var halfHeight = up * (height / 2);

        var topLeft = position - halfWidth + halfHeight;
        var topRight = position + halfWidth + halfHeight;
        var bottomRight = position + halfWidth - halfHeight;
        var bottomLeft = position - halfWidth - halfHeight;

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
        
        // Enable color key shader for transparency
        EnsureColorKeyShader();
        if (_colorKeyShader.HasValue)
        {
            BeginShaderMode(_colorKeyShader.Value);
        }
        
        // Draw the quad
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);

        // Calculate normal (facing camera)
        var normal = directionToCamera;
        Rlgl.Normal3f(normal.X, normal.Y, normal.Z);

        // Top-left (flip X texture coordinate to fix Y-axis flip)
        Rlgl.TexCoord2f(texRight, texTop);
        Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);

        // Top-right
        Rlgl.TexCoord2f(texLeft, texTop);
        Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);

        // Bottom-right
        Rlgl.TexCoord2f(texLeft, texBottom);
        Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);

        // Bottom-left
        Rlgl.TexCoord2f(texRight, texBottom);
        Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);

        Rlgl.End();
        Rlgl.SetTexture(0);
        
        // Disable shader
        if (_colorKeyShader.HasValue)
        {
            EndShaderMode();
        }
        
        LevelData.DrawedQuads += 1;
    }
}

