#version 100

precision mediump float;

// Input vertex attributes (from vertex shader)
varying vec2 fragTexCoord;
varying vec4 fragColor;

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Color key uniform (RGB values in 0-255 range)
uniform vec3 colorKey;

void main()
{
    vec4 texColor = texture2D(texture0, fragTexCoord);
    
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
    
    gl_FragColor = texColor * colDiffuse * fragColor;
}
