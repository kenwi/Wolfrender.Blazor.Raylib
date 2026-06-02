// Included before void main() in scene fragment shaders (GLSL ES 1.00).

float exponentialFalloffBrightness(float distance, float maxDistance, float minBright)
{
    if (distance <= 0.0 || maxDistance <= 0.0)
        return 1.0;

    float falloffFactor = maxDistance / 3.0;
    float expBrightness = exp(-distance / falloffFactor);
    float brightness = expBrightness * (1.0 - minBright) + minBright;
    return clamp(brightness, minBright, 1.0);
}

float lightContributionAt(vec3 worldPos, vec3 lightPos, float radius)
{
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
