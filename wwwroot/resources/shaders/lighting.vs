#version 100

// Input vertex attributes
attribute vec3 vertexPosition;
attribute vec2 vertexTexCoord;
attribute vec3 vertexNormal;
attribute vec4 vertexColor;

// Outputs to fragment shader
varying vec2 fragTexCoord;
varying vec4 fragColor;
varying vec3 fragWorldPos;

// Uniforms
uniform mat4 mvp;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    
    // Vertex position is already in world space (we pass world coords directly)
    fragWorldPos = vertexPosition;
    
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
