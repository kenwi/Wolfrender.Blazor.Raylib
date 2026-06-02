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

void main()
{
    vec4 texColor = texture2D(texture0, fragTexCoord);
    float brightness = combinedSceneBrightness(fragWorldPos);
    vec3 litColor = texColor.rgb * brightness;
    gl_FragColor = vec4(litColor, texColor.a) * colDiffuse * fragColor;
}
