using System.Numerics;

namespace Game.Utilities;

/// <summary>
/// World-space quad corners for camera-facing billboards (matches <see cref="PrimitiveRenderer.DrawSpriteTexture"/>).
/// </summary>
public static class SpriteBillboardGeometry
{
    /// <summary>
    /// Horizontal facing direction for a vertical billboard (Y-up, XZ plane).
    /// When <paramref name="quantizeToEightDirections"/> is true, snaps to 45° steps (Wolfenstein-style).
    /// </summary>
    public static Vector3 ComputeBillboardFacingDirection(
        Vector3 position,
        Vector3 cameraPosition,
        bool quantizeToEightDirections = true)
    {
        var directionToCamera = cameraPosition - position;
        directionToCamera.Y = 0;

        var dirLength = directionToCamera.Length();
        if (dirLength < 0.001f)
            directionToCamera = new Vector3(0, 0, 1);
        else
            directionToCamera /= dirLength;

        if (!quantizeToEightDirections)
            return directionToCamera;

        float angleRad = MathF.Atan2(directionToCamera.X, directionToCamera.Z);
        if (angleRad < 0)
            angleRad += 2f * MathF.PI;

        float quantizedAngleRad = MathF.Round(angleRad / (MathF.PI / 4f)) * (MathF.PI / 4f);
        return new Vector3(MathF.Sin(quantizedAngleRad), 0, MathF.Cos(quantizedAngleRad));
    }

    /// <summary>
    /// Computes the four corners of a vertical billboard facing the camera (Y-up, XZ billboard plane).
    /// </summary>
    public static void ComputeBillboardQuad(
        Vector3 position,
        Vector3 cameraPosition,
        float width,
        float height,
        float yAxisAngleRadians,
        out Vector3 topLeft,
        out Vector3 topRight,
        out Vector3 bottomRight,
        out Vector3 bottomLeft,
        bool quantizeToEightDirections = true)
    {
        var directionToCamera = ComputeBillboardFacingDirection(
            position, cameraPosition, quantizeToEightDirections);

        var right = Vector3.Cross(directionToCamera, Vector3.UnitY);
        float rightLength = right.Length();
        if (rightLength > 0.001f)
            right /= rightLength;
        else
            right = Vector3.UnitX;

        var up = Vector3.UnitY;

        float cosAngle = MathF.Cos(yAxisAngleRadians);
        float sinAngle = MathF.Sin(yAxisAngleRadians);
        var rotatedRight = new Vector3(
            right.X * cosAngle - right.Z * sinAngle,
            right.Y,
            right.X * sinAngle + right.Z * cosAngle);

        var halfWidth = rotatedRight * (width / 2f);
        var halfHeight = up * (height / 2f);

        topLeft = position - halfWidth + halfHeight;
        topRight = position + halfWidth + halfHeight;
        bottomRight = position + halfWidth - halfHeight;
        bottomLeft = position - halfWidth - halfHeight;
    }
}
