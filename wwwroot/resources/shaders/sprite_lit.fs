#version 100

precision mediump float;

varying vec2 fragTexCoord;
varying vec4 fragColor;
varying vec3 fragWorldPos;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform vec3 playerPosition;
uniform float maxLightDistance;
uniform float minBrightness;

// Magenta color key (#980088), same as transparency.fs
uniform vec3 colorKey;

void main()
{
    vec4 texColor = texture2D(texture0, fragTexCoord);

    vec3 texRGB = texColor.rgb * 255.0;
    float tolerance = 5.0;
    if (abs(texRGB.r - colorKey.r) < tolerance &&
        abs(texRGB.g - colorKey.g) < tolerance &&
        abs(texRGB.b - colorKey.b) < tolerance)
    {
        discard;
    }

    float distance = length(fragWorldPos - playerPosition);
    float brightness = 1.0;
    if (distance > 0.0 && maxLightDistance > 0.0)
    {
        float falloffFactor = maxLightDistance / 3.0;
        float expBrightness = exp(-distance / falloffFactor);
        brightness = expBrightness * (1.0 - minBrightness) + minBrightness;
        brightness = clamp(brightness, minBrightness, 1.0);
    }

    vec3 litColor = texColor.rgb * brightness;
    gl_FragColor = vec4(litColor, texColor.a) * colDiffuse * fragColor;
}
