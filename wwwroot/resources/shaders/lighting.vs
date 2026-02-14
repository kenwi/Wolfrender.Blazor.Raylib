#version 330

// Input vertex attributes
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

// Outputs to fragment shader
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragWorldPos;

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