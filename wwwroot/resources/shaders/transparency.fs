#version 330

// Input vertex attributes (from vertex shader)
in vec2 fragTexCoord;
in vec4 fragColor;

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Output fragment color
out vec4 finalColor;

// Color key uniform (RGB values in 0-255 range)
uniform vec3 colorKey;

void main()
{
    vec4 texColor = texture(texture0, fragTexCoord);
    
    // Convert texture color to 0-255 range for comparison
    vec3 texRGB = texColor.rgb * 255.0;
    
    // Check if color matches the key color (with small tolerance for compression artifacts)
    float tolerance = 5.0;
    if (abs(texRGB.r - colorKey.r) < tolerance &&
        abs(texRGB.g - colorKey.g) < tolerance &&
        abs(texRGB.b - colorKey.b) < tolerance)
    {
        discard; // Make pixel transparent
    }
    
    finalColor = texColor * colDiffuse * fragColor;
}