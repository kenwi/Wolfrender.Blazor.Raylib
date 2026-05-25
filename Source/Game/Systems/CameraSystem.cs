using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class CameraSystem
{
    private const float DeathFallDuration = 1.1f;
    private const float DeathFloorEyeOffsetY = -1.55f;
    private const float DeathLookDistance = 3f;

    private readonly CollisionSystem _collisionSystem;
    private readonly float _defaultMouseSensitivity = 0.003f;
    private float _mouseSensitivity;

    private bool _deathFallActive;
    private float _deathFallT;
    private Vector3 _deathBaseForward = new(0f, 0f, -1f);

    public CameraSystem(CollisionSystem collisionSystem)
    {
        _collisionSystem = collisionSystem;
        _mouseSensitivity = _defaultMouseSensitivity;
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        _mouseSensitivity = _defaultMouseSensitivity * sensitivity;
    }

    public void ResetDeathFall()
    {
        _deathFallActive = false;
        _deathFallT = 0f;
    }

    /// <summary>Collapse the view toward the floor after the player dies.</summary>
    public void UpdateDeathFall(Player player, float deltaTime)
    {
        if (!_deathFallActive)
        {
            _deathFallActive = true;
            _deathFallT = 0f;

            var cam = player.Camera;
            Vector3 initialForward = cam.Target - cam.Position;
            if (initialForward.LengthSquared() > 0.0001f)
                _deathBaseForward = Vector3.Normalize(initialForward);
        }

        _deathFallT = MathF.Min(1f, _deathFallT + deltaTime / DeathFallDuration);
        float t = 1f - (1f - _deathFallT) * (1f - _deathFallT);

        Vector3 horizontal = new Vector3(_deathBaseForward.X, 0f, _deathBaseForward.Z);
        if (horizontal.LengthSquared() < 0.0001f)
            horizontal = new Vector3(0f, 0f, -1f);
        else
            horizontal = Vector3.Normalize(horizontal);

        Vector3 lookForward = Vector3.Normalize(horizontal + new Vector3(0f, -2.5f * t, 0f));

        var camera = player.Camera;
        camera.Position = player.Position + new Vector3(0f, DeathFloorEyeOffsetY * t, 0f);
        camera.Target = camera.Position + lookForward * DeathLookDistance;
        camera.Up = Vector3.UnitY;
        player.Camera = camera;
    }

    public void Update(Player player, bool isMouseFree, Vector2 mouseDelta)
    {
        var camera = player.Camera;

        // Get current look direction before any updates
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        float lookDistance = Vector3.Distance(camera.Target, camera.Position);
        if (lookDistance < 0.001f)
            lookDistance = 1.0f;

        // Handle rotation from mouse input (only if not mouse-free)
        if (!isMouseFree && (Math.Abs(mouseDelta.X) > 0.001f || Math.Abs(mouseDelta.Y) > 0.001f))
        {
            // Calculate rotation angles from mouse delta
            float yaw = -mouseDelta.X * _mouseSensitivity;
            float pitch = -mouseDelta.Y * _mouseSensitivity;

            // Apply yaw (horizontal rotation around up axis)
            Matrix4x4 yawMatrix = Matrix4x4.CreateFromAxisAngle(camera.Up, yaw);
            forward = Vector3.Transform(forward, yawMatrix);

            // Apply pitch (vertical rotation) - limit pitch to avoid gimbal lock
            Vector3 pitchAxis = Vector3.Cross(forward, camera.Up);
            if (pitchAxis.Length() > 0.001f)
            {
                pitchAxis = Vector3.Normalize(pitchAxis);
                Matrix4x4 pitchMatrix = Matrix4x4.CreateFromAxisAngle(pitchAxis, pitch);
                Vector3 newForward = Vector3.Transform(forward, pitchMatrix);

                // Limit pitch to prevent flipping (check before applying)
                float dot = Vector3.Dot(newForward, camera.Up);
                if (Math.Abs(dot) < 0.98f)
                {
                    forward = newForward;
                }
            }
        }

        // Sync camera position with player position (after collision resolution)
        camera.Position = player.Position;
        
        // Set target based on position and look direction
        camera.Target = camera.Position + forward * lookDistance;

        // Sync back
        player.Camera = camera;
    }
}

