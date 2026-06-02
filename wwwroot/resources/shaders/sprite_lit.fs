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

uniform float tileLightCount;
uniform vec3 tileLight0;
uniform vec3 tileLight1;
uniform vec3 tileLight2;
uniform vec3 tileLight3;
uniform vec3 tileLight4;
uniform vec3 tileLight5;
uniform vec3 tileLight6;
uniform vec3 tileLight7;
uniform float tileLightRadius;

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

    float brightness = combinedSceneBrightness(fragWorldPos);
    vec3 litColor = texColor.rgb * brightness;
    gl_FragColor = vec4(litColor, texColor.a) * colDiffuse * fragColor;
}
