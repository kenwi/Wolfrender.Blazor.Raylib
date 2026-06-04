using System.Numerics;

namespace Game.Engine.Rendering;

/// <summary>
/// World-space quad corners for camera-facing billboards (matches <see cref="PrimitiveRenderer.DrawSpriteTexture"/>).
/// </summary>
public static class SpriteBillboardGeometry
{
    /// <summary>
    /// How the billboard plane is oriented in the horizontal (XZ) plane.
    /// </summary>
    public enum FacingMode
    {
        /// <summary>Each sprite faces the camera position (enemies; optional 8-direction snap).</summary>
        PointAtCamera,

        /// <summary>
        /// All sprites share the camera view yaw (plane perpendicular to view forward).
        /// Used for pickups and placed objects so they stay head-on to the player view.
        /// </summary>
        ViewAligned
    }

    /// <summary>
    /// Builds horizontal facing direction and right tangent for a vertical billboard (Y-up).
    /// </summary>
    public static void ComputeBillboardBasis(
        Vector3 spritePosition,
        Vector3 cameraPosition,
        Vector3? cameraViewTarget,
        FacingMode facingMode,
        bool quantizeToEightDirections,
        out Vector3 facingDirection,
        out Vector3 right)
    {
        Vector3 dir;
        if (facingMode == FacingMode.ViewAligned && cameraViewTarget.HasValue)
        {
            dir = cameraViewTarget.Value - cameraPosition;
            quantizeToEightDirections = false;
        }
        else
        {
            dir = cameraPosition - spritePosition;
        }

        dir.Y = 0;
        float len = dir.Length();
        if (len < 0.001f)
            dir = new Vector3(0, 0, 1);
        else
            dir /= len;

        if (quantizeToEightDirections)
            dir = QuantizeToEightDirections(dir);

        facingDirection = dir;
        right = ComputeRightTangent(dir, facingMode);
    }

    /// <summary>
    /// Horizontal facing direction for a vertical billboard (Y-up, XZ plane).
    /// When <paramref name="quantizeToEightDirections"/> is true, snaps to 45° steps (Wolfenstein-style).
    /// </summary>
    public static Vector3 ComputeBillboardFacingDirection(
        Vector3 position,
        Vector3 cameraPosition,
        bool quantizeToEightDirections = true)
    {
        ComputeBillboardBasis(
            position,
            cameraPosition,
            cameraViewTarget: null,
            FacingMode.PointAtCamera,
            quantizeToEightDirections,
            out Vector3 facing,
            out _);
        return facing;
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
        ComputeBillboardBasis(
            position,
            cameraPosition,
            cameraViewTarget: null,
            FacingMode.PointAtCamera,
            quantizeToEightDirections,
            out _,
            out Vector3 right);

        BuildQuadCorners(position, right, width, height, yAxisAngleRadians,
            out topLeft, out topRight, out bottomRight, out bottomLeft);
    }

    public static void BuildQuadCorners(
        Vector3 position,
        Vector3 right,
        float width,
        float height,
        float yAxisAngleRadians,
        out Vector3 topLeft,
        out Vector3 topRight,
        out Vector3 bottomRight,
        out Vector3 bottomLeft)
    {
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

    private static Vector3 ComputeRightTangent(Vector3 horizontalFacing, FacingMode facingMode)
    {
        // Winding must match the original DrawSpriteTexture layout per mode or backface culling hides the quad.
        var right = facingMode == FacingMode.ViewAligned
            ? Vector3.Cross(Vector3.UnitY, horizontalFacing)
            : Vector3.Cross(horizontalFacing, Vector3.UnitY);

        float rightLength = right.Length();
        if (rightLength > 0.001f)
            return right / rightLength;

        return Vector3.UnitX;
    }

    private static Vector3 QuantizeToEightDirections(Vector3 direction)
    {
        float angleRad = MathF.Atan2(direction.X, direction.Z);
        if (angleRad < 0)
            angleRad += 2f * MathF.PI;

        float quantizedAngleRad = MathF.Round(angleRad / (MathF.PI / 4f)) * (MathF.PI / 4f);
        return new Vector3(MathF.Sin(quantizedAngleRad), 0, MathF.Cos(quantizedAngleRad));
    }
}
