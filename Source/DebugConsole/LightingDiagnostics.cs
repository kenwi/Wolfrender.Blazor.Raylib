using System.Numerics;
using Game.Core.Level;
using Game.Engine.Rendering;
using Game.Features.WorldObjects;

namespace Game.DebugConsole;

/// <summary>CPU-side lighting pipeline report for the <c>lightcheck</c> console command.</summary>
public static class LightingDiagnostics
{
    public static List<string> BuildReport(
        MapData mapData,
        LevelRoomMap roomMap,
        IDoorPortalState portals,
        Vector3 playerPosition,
        LightOcclusionMap occlusionMap,
        LightingDebugSnapshot shaderState)
    {
        var rows = new List<string>();
        var (entityTileX, entityTileY) = LevelData.GetEntityTileFromWorld(playerPosition.X, playerPosition.Z);
        var (worldTileX, worldTileY) = LevelData.GetTileFromWorld(playerPosition.X, playerPosition.Z);
        int playerRoom = roomMap.GetRoomAt(entityTileX, entityTileY);
        int tileRoomId = roomMap.TileRoomId[LevelData.GetIndex(entityTileX, entityTileY, mapData.Width)];
        int textureRoomId = occlusionMap.DecodeRoomId(entityTileX, entityTileY);
        byte roomPixel = occlusionMap.GetRoomPixel(entityTileX, entityTileY);

        var visibleRooms = roomMap.ComputeVisibleRooms(entityTileX, entityTileY, portals);
        var mapLights = TileLightCollector.Collect(mapData);
        var activeLights = TileLightCollector.SelectForVisibleRooms(
            mapLights,
            roomMap,
            visibleRooms,
            playerPosition,
            LightObjectEncoding.MaxShaderLights);

        Vector3 floorSample = new(playerPosition.X, 0f, playerPosition.Z);

        rows.Add("--- Player ---");
        rows.Add($"position=({playerPosition.X:F2},{playerPosition.Y:F2},{playerPosition.Z:F2})");
        rows.Add($"entityTile=({entityTileX},{entityTileY}) worldTile=({worldTileX},{worldTileY})");
        rows.Add($"roomAtPlayer={playerRoom} tileRoomId={tileRoomId} textureRoom={textureRoomId} roomPixel={roomPixel}");
        rows.Add($"gpuRoomReadback={occlusionMap.ReadBackRoomIdFromGpu(entityTileX, entityTileY)}");
        rows.Add($"floorOcclusionBlocks={occlusionMap.TileBlocksAt(entityTileX, entityTileY)}");
        rows.Add($"playerTorchEst={EstimateTorchBrightness(floorSample, playerPosition, shaderState):F3}");

        rows.Add("--- Room visibility ---");
        rows.Add($"roomCount={roomMap.RoomCount} visibleRooms=[{string.Join(",", visibleRooms.OrderBy(id => id))}]");
        rows.Add($"doorLinks={roomMap.DoorLinks.Count} roomMapTiles={roomMap.EnumerateRoomTiles().Count()}");

        rows.Add("--- Map lights ---");
        int inVisibleRoom = 0;
        foreach (var light in mapLights)
        {
            if (IsLightInVisibleRooms(roomMap, visibleRooms, light.TileX, light.TileY))
                inVisibleRoom++;
        }

        rows.Add($"totalFixtures={mapLights.Count} inVisibleRooms={inVisibleRoom} shaderMax={PrimitiveRenderer.MaxShaderLights}");
        rows.Add($"selectedForShader={activeLights.Length} uploaded={shaderState.ActiveUploadedLightCount}");

        rows.Add("--- Shader / uniforms ---");
        rows.Add($"lightingShader valid={shaderState.LightingShaderValid} programId={shaderState.LightingShaderProgramId}");
        rows.Add($"spriteLitShader valid={shaderState.SpriteLitShaderValid} programId={shaderState.SpriteLitShaderProgramId}");
        rows.Add(FormatUniform("tileLightCount", shaderState.TileLightCountLoc));
        rows.Add(FormatUniform("occlusionMap", shaderState.OcclusionMapLoc));
        rows.Add(FormatUniform("roomMap", shaderState.RoomMapLoc));
        rows.Add(FormatUniform("mapSize", shaderState.MapSizeLoc));
        rows.Add(FormatUniform("tileSize", shaderState.TileSizeLoc));
        rows.Add(FormatUniform("applySurfaceLighting", shaderState.ApplySurfaceLightingLoc));
        rows.Add(FormatUniform("meshRoomId", shaderState.MeshRoomIdLoc));
        rows.Add($"textureUnits: occlusion={PrimitiveRenderer.OcclusionTextureUnitForDebug} room={PrimitiveRenderer.RoomTextureUnitForDebug}");
        rows.Add($"params: maxDist={shaderState.MaxLightDistance:F1} minBright={shaderState.MinBrightness:F3} tileRadius={shaderState.TileLightRadius:F1}");

        rows.Add("--- GPU textures ---");
        rows.Add($"occlusionMap {occlusionMap.DescribeTextures()} blockingTiles={occlusionMap.CountBlockingTiles()}");
        rows.Add($"roomMap encodedTiles={occlusionMap.CountEncodedRoomTiles()} textureId={shaderState.RoomTextureId}");

        rows.Add("--- Active lights ---");
        if (activeLights.Length == 0)
        {
            rows.Add("(none selected - check visible rooms and fixture placement)");
        }
        else
        {
            for (int i = 0; i < activeLights.Length; i++)
            {
                var light = activeLights[i];
                var (lightTileX, lightTileY) = LevelData.GetTileFromWorld(light.Position.X, light.Position.Z);
                float dist = DistanceXZ(light.Position, floorSample);
                bool roomMatch = RoomsMatch(light.RoomA, light.RoomB, textureRoomId);
                bool blocked = LightPathBlocked(occlusionMap, light.Position, floorSample);
                bool inRange = dist <= shaderState.TileLightRadius;
                float fixtureBright = roomMatch && !blocked && inRange
                    ? EstimateFixtureBrightness(dist, shaderState.TileLightRadius, shaderState.MinBrightness)
                    : 0f;

                string reject = BuildRejectReason(roomMatch, blocked, inRange, textureRoomId, light.RoomA, light.RoomB);
                rows.Add(
                    $"#{i} tile=({lightTileX},{lightTileY}) pos=({light.Position.X:F1},{light.Position.Y:F1},{light.Position.Z:F1}) " +
                    $"rooms=({light.RoomA},{light.RoomB}) dist={dist:F1} bright={fixtureBright:F3} {reject}");
            }
        }

        rows.Add("--- Uploaded vs selected ---");
        if (shaderState.ActiveLights.Length != activeLights.Length)
        {
            rows.Add($"WARN: shader uploaded {shaderState.ActiveLights.Length} lights, selection produced {activeLights.Length}");
        }

        for (int i = 0; i < shaderState.ActiveLights.Length; i++)
        {
            var uploaded = shaderState.ActiveLights[i];
            var rooms = shaderState.ActiveLightRooms[i];
            int lightLoc = i < shaderState.TileLightLocs.Length ? shaderState.TileLightLocs[i] : -1;
            int roomLoc = i < shaderState.TileLightRoomLocs.Length ? shaderState.TileLightRoomLocs[i] : -1;
            rows.Add(
                $"upload#{i} pos=({uploaded.X:F1},{uploaded.Y:F1},{uploaded.Z:F1}) rooms=({rooms.X:F0},{rooms.Y:F0}) " +
                $"locs light={FormatLoc(lightLoc)} room={FormatLoc(roomLoc)}");
        }

        if (shaderState.OcclusionMapLoc < 0 || shaderState.RoomMapLoc < 0)
            rows.Add("WARN: occlusionMap or roomMap uniform missing - placed lights will not work in shader.");

        if (shaderState.TileLightRoomLocs.Any(loc => loc < 0))
            rows.Add("WARN: one or more tileLightRoom uniforms missing - room matching defaults to (0,0).");

        if (occlusionMap.CountEncodedRoomTiles() == 0)
            rows.Add("WARN: room map texture has no encoded tiles - fragment room will always be -1.");

        return rows;
    }

    private static bool IsLightInVisibleRooms(
        LevelRoomMap roomMap,
        IReadOnlySet<int> visibleRooms,
        int tileX,
        int tileY)
    {
        foreach (int roomId in roomMap.GetTileRoomIds(tileX, tileY))
        {
            if (roomId >= 0 && visibleRooms.Contains(roomId))
                return true;
        }

        return false;
    }

    private static string BuildRejectReason(
        bool roomMatch,
        bool blocked,
        bool inRange,
        int fragRoom,
        int roomA,
        int roomB)
    {
        if (roomMatch && !blocked && inRange)
            return "ok";

        var parts = new List<string>();
        if (!roomMatch)
            parts.Add($"roomMismatch(frag={fragRoom} light=({roomA},{roomB}))");
        if (blocked)
            parts.Add("pathBlocked");
        if (!inRange)
            parts.Add("outOfRange");

        return string.Join(" ", parts);
    }

    private static bool RoomsMatch(int roomA, int roomB, int fragRoom)
    {
        if (fragRoom < 0)
            return false;

        if (roomA == fragRoom)
            return true;

        return roomB >= 0 && roomB == fragRoom;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static float EstimateTorchBrightness(Vector3 sample, Vector3 playerPos, LightingDebugSnapshot state) =>
        EstimateFixtureBrightness(DistanceXZ(sample, playerPos), state.MaxLightDistance, state.MinBrightness);

    private static float EstimateFixtureBrightness(float distance, float radius, float minBright)
    {
        if (distance <= 0f || radius <= 0f)
            return 1f;

        float falloffFactor = radius / 3f;
        float expBrightness = MathF.Exp(-distance / falloffFactor);
        float brightness = expBrightness * (1f - minBright) + minBright;
        return Math.Clamp(brightness, minBright, 1f);
    }

    private static string FormatUniform(string name, int location) =>
        $"{name} loc={FormatLoc(location)}";

    private static string FormatLoc(int location) =>
        location >= 0 ? location.ToString() : "NOT_FOUND";

    private static bool LightPathBlocked(LightOcclusionMap map, Vector3 fromWorld, Vector3 toWorld)
    {
        float tileSize = LevelData.QuadSize;
        float posX = fromWorld.X / tileSize;
        float posY = fromWorld.Z / tileSize;
        float targetX = toWorld.X / tileSize;
        float targetY = toWorld.Z / tileSize;

        float deltaX = targetX - posX;
        float deltaY = targetY - posY;
        float maxDist = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (maxDist < 0.001f)
            return false;

        float dirX = deltaX / maxDist;
        float dirY = deltaY / maxDist;
        int mapX = (int)MathF.Floor(posX);
        int mapY = (int)MathF.Floor(posY);
        int targetTileX = (int)MathF.Floor(targetX);
        int targetTileY = (int)MathF.Floor(targetY);

        int stepX = dirX >= 0f ? 1 : -1;
        int stepY = dirY >= 0f ? 1 : -1;

        float deltaDistX = MathF.Abs(dirX) > 0.0001f ? MathF.Abs(1f / dirX) : 10000f;
        float deltaDistY = MathF.Abs(dirY) > 0.0001f ? MathF.Abs(1f / dirY) : 10000f;

        float sideDistX = dirX < 0f
            ? (posX - mapX) * deltaDistX
            : (mapX + 1f - posX) * deltaDistX;
        float sideDistY = dirY < 0f
            ? (posY - mapY) * deltaDistY
            : (mapY + 1f - posY) * deltaDistY;

        for (int i = 0; i < 128; i++)
        {
            if (mapX == targetTileX && mapY == targetTileY)
                break;

            if (sideDistX < sideDistY)
            {
                sideDistX += deltaDistX;
                mapX += stepX;
            }
            else
            {
                sideDistY += deltaDistY;
                mapY += stepY;
            }

            if (map.TileBlocksAt(mapX, mapY))
                return true;
        }

        return false;
    }
}
