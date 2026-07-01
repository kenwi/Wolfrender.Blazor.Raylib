// Included before void main() in scene fragment shaders (GLSL ES 1.00).

uniform sampler2D occlusionMap;
uniform vec2 mapSize;
uniform float tileSize;

varying vec3 fragNormal;

float exponentialFalloffBrightness(float distance, float maxDistance, float minBright)
{
    if (distance <= 0.0 || maxDistance <= 0.0)
        return 1.0;

    float falloffFactor = maxDistance / 3.0;
    float expBrightness = exp(-distance / falloffFactor);
    float brightness = expBrightness * (1.0 - minBright) + minBright;
    return clamp(brightness, minBright, 1.0);
}

float sampleOcclusion(vec2 tileXY)
{
    vec2 uv = (tileXY + vec2(0.5)) / mapSize;
    return texture2D(occlusionMap, uv).r;
}

bool tileBlocksLight(vec2 tileXY)
{
    if (tileXY.x < 0.0 || tileXY.y < 0.0 || tileXY.x >= mapSize.x || tileXY.y >= mapSize.y)
        return true;

    return sampleOcclusion(tileXY) > 0.5;
}

bool lightPathBlocked(vec3 fromWorld, vec3 toWorld)
{
    vec2 pos = fromWorld.xz / tileSize;
    vec2 target = toWorld.xz / tileSize;
    vec2 delta = target - pos;
    float maxDist = length(delta);
    if (maxDist < 0.001)
        return false;

    vec2 dir = delta / maxDist;
    int mapX = int(floor(pos.x));
    int mapY = int(floor(pos.y));
    int targetX = int(floor(target.x));
    int targetY = int(floor(target.y));

    int stepX = dir.x >= 0.0 ? 1 : -1;
    int stepY = dir.y >= 0.0 ? 1 : -1;

    float deltaDistX = abs(dir.x) > 0.0001 ? abs(1.0 / dir.x) : 10000.0;
    float deltaDistY = abs(dir.y) > 0.0001 ? abs(1.0 / dir.y) : 10000.0;

    float sideDistX;
    float sideDistY;
    if (dir.x < 0.0)
        sideDistX = (pos.x - float(mapX)) * deltaDistX;
    else
        sideDistX = (float(mapX) + 1.0 - pos.x) * deltaDistX;

    if (dir.y < 0.0)
        sideDistY = (pos.y - float(mapY)) * deltaDistY;
    else
        sideDistY = (float(mapY) + 1.0 - pos.y) * deltaDistY;

    float dist = 0.0;
    for (int i = 0; i < 128; i++)
    {
        if (dist > maxDist)
            break;

        if (mapX == targetX && mapY == targetY)
            break;

        if (sideDistX < sideDistY)
        {
            dist = sideDistX;
            sideDistX += deltaDistX;
            mapX += stepX;
        }
        else
        {
            dist = sideDistY;
            sideDistY += deltaDistY;
            mapY += stepY;
        }

        if (tileBlocksLight(vec2(float(mapX), float(mapY))))
            return true;
    }

    return false;
}

float lightContributionAt(vec3 worldPos, vec3 lightPos, float radius)
{
    vec3 toLight = lightPos - worldPos;
    if (dot(normalize(toLight), normalize(fragNormal)) <= 0.0)
        return 0.0;

    if (lightPathBlocked(lightPos, worldPos))
        return 0.0;

    vec2 delta = vec2(worldPos.x - lightPos.x, worldPos.z - lightPos.z);
    float distance = length(delta);
    return exponentialFalloffBrightness(distance, radius, minBrightness);
}

float playerLightBrightness(vec3 worldPos)
{
    float distance = length(worldPos - playerPosition);
    return exponentialFalloffBrightness(distance, maxLightDistance, minBrightness);
}

float tileLightBrightness(vec3 worldPos)
{
    float best = 0.0;

    if (tileLightCount >= 1.0)
        best = max(best, lightContributionAt(worldPos, tileLight0, tileLightRadius));
    if (tileLightCount >= 2.0)
        best = max(best, lightContributionAt(worldPos, tileLight1, tileLightRadius));
    if (tileLightCount >= 3.0)
        best = max(best, lightContributionAt(worldPos, tileLight2, tileLightRadius));
    if (tileLightCount >= 4.0)
        best = max(best, lightContributionAt(worldPos, tileLight3, tileLightRadius));
    if (tileLightCount >= 5.0)
        best = max(best, lightContributionAt(worldPos, tileLight4, tileLightRadius));
    if (tileLightCount >= 6.0)
        best = max(best, lightContributionAt(worldPos, tileLight5, tileLightRadius));
    if (tileLightCount >= 7.0)
        best = max(best, lightContributionAt(worldPos, tileLight6, tileLightRadius));
    if (tileLightCount >= 8.0)
        best = max(best, lightContributionAt(worldPos, tileLight7, tileLightRadius));

    return best;
}

float combinedSceneBrightness(vec3 worldPos)
{
    return clamp(max(playerLightBrightness(worldPos), tileLightBrightness(worldPos)), minBrightness, 1.0);
}
