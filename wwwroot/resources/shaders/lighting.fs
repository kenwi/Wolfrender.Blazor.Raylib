#version 100

precision mediump float;

// Input vertex attributes (from vertex shader)
varying vec2 fragTexCoord;
varying vec4 fragColor;
varying vec3 fragWorldPos; // World position passed from vertex shader

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Lighting uniforms
uniform vec3 playerPosition;      // Player position in world space
uniform float maxLightDistance;   // Maximum distance for full brightness
uniform float minBrightness;      // Minimum brightness (0-1)

void main()
{
    vec4 texColor = texture2D(texture0, fragTexCoord);
    
    // Calculate distance from player to this pixel's world position
    float distance = length(fragWorldPos - playerPosition);
    
    // Calculate brightness based on distance (exponential falloff)
    // Uses exponential decay: brightness = e^(-distance/falloffFactor)
    // At distance 0: brightness = 1.0
    // As distance increases: brightness decays exponentially
    float brightness = 1.0;
    if (distance > 0.0 && maxLightDistance > 0.0)
    {
        // Exponential falloff: exp(-distance / (maxLightDistance / 1.0))
        // The division by 3.0 makes the falloff happen over a reasonable range
        // At maxLightDistance: exp(-3) ≈ 0.05, so we scale and add minBrightness
        float falloffFactor = maxLightDistance / 3.0;
        float expBrightness = exp(-distance / falloffFactor);
        
        // Scale from [0, 1] to [minBrightness, 1.0]
        brightness = expBrightness * (1.0 - minBrightness) + minBrightness;
        brightness = clamp(brightness, minBrightness, 1.0);
    }
    
    // Apply brightness to color
    vec3 litColor = texColor.rgb * brightness;
    
    gl_FragColor = vec4(litColor, texColor.a) * colDiffuse * fragColor;
}
