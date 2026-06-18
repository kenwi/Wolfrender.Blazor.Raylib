#version 100

// Screen-space sprite vertex shader (DrawTexturePro / DrawScreenSprite).
// Must stay on GLSL ES 1.00 so desktop and WASM link with transparency.fs.

attribute vec3 vertexPosition;
attribute vec2 vertexTexCoord;
attribute vec4 vertexColor;

varying vec2 fragTexCoord;
varying vec4 fragColor;

uniform mat4 mvp;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
