#ifndef MYSCREEN_SPILL_INCLUDED
#define MYSCREEN_SPILL_INCLUDED

// MyScreen's spill: the screen's picture thrown onto the room as extra light, the way a cinema screen lights its
// seats. A rectangular throw from the screen (centre, facing, half width and height), widening with distance so
// floors, ceilings and walls get picture detail, fading out at _MyScreenSpillRadius.
//
// Every value is a shader global, set by the MyScreen Spill rig's hub graph (Editor/MyScreenSpillRig.cs). Nothing is
// set per material: a material only has to use "MyScreen/Lit Spill" (or include this file and add
// EvaluateMyScreenSpill to its lighting). See README.md and AGENTS.md for the contract.
//
//   _MyScreenSpillTex        the capture of the screen's picture (the rig's render texture, with mips)
//   _MyScreenSpillCenter     xyz: the screen's centre, world space
//   _MyScreenSpillNormal     xyz: the way the screen faces (towards the audience), unit
//   _MyScreenSpillRight      xyz: the screen's right, unit; w: half its width, metres
//   _MyScreenSpillUp         xyz: the screen's up, unit; w: half its height, metres
//   _MyScreenSpillRadius     metres: no light beyond this
//   _MyScreenSpillIntensity  strength (0 switches the spill off)
//   _MyScreenSpillMip        the mip level sampled: the smear (higher is softer and cheaper)
//   _MyScreenSpillFlat       1: the picture is 2D, its whole frame lights the room; 0: half-SBS 3D, the left eye only

TEXTURE2D(_MyScreenSpillTex);

// The spill declares no sampler of its own: URP Lit can already use all 16 sampler slots in some variants (a 17th
// fails to compile on D3D11, GLES3 and Vulkan). It borrows the material's base map sampler; a shader without
// sampler_BaseMap defines MYSCREEN_SPILL_SAMPLER to one it has, before including this file.
#ifndef MYSCREEN_SPILL_SAMPLER
    #define MYSCREEN_SPILL_SAMPLER sampler_BaseMap
#endif

float4 _MyScreenSpillCenter;
float4 _MyScreenSpillNormal;
float4 _MyScreenSpillRight;
float4 _MyScreenSpillUp;
float _MyScreenSpillRadius;
float _MyScreenSpillIntensity;
float _MyScreenSpillMip;
float _MyScreenSpillFlat;

// 1.0 = no spread (the screen's own footprint). Above 1.0 the throw opens with distance onto floor, ceiling and walls.
#define MYSCREEN_SPILL_SPREAD 4.0

float MyScreenSpillFalloff(float dist, float radius)
{
    float t = saturate(1.0 - dist / radius);
    return t * t * (3.0 - 2.0 * t);
}

// The spill's light at a surface point with this normal (both world space). Add it to the surface's irradiance
// (URP: inputData.bakedGI), so its albedo and occlusion still apply.
half3 EvaluateMyScreenSpill(float3 worldPos, float3 worldNorm)
{
    float radius = _MyScreenSpillRadius;
    float halfWidth = _MyScreenSpillRight.w;
    float halfHeight = _MyScreenSpillUp.w;
    float intensity = _MyScreenSpillIntensity;
    if (radius <= 0.0001 || halfWidth <= 0.0001 || halfHeight <= 0.0001 || intensity <= 0.0001)
        return (half3)0;

    float3 center = _MyScreenSpillCenter.xyz;
    float3 normal = _MyScreenSpillNormal.xyz;
    float3 rightUnit = _MyScreenSpillRight.xyz;
    float3 upUnit = _MyScreenSpillUp.xyz;

    float3 rel = worldPos - center;
    float dist = length(rel);
    if (dist < 0.0001)
        return (half3)0;
    float3 relN = rel / dist;

    // Only in front of the screen, and only on surfaces turned towards it.
    float screenFacing = dot(normal, relN);
    if (screenFacing <= 0.0001)
        return (half3)0;
    float ndotl = saturate(-dot(worldNorm, relN));
    if (ndotl < 0.0001)
        return (half3)0;
    float z = dot(rel, normal);
    if (z <= 0.0001)
        return (half3)0;

    float spreadSlope = max(MYSCREEN_SPILL_SPREAD - 1.0, 0.0);
    float extentX = halfWidth + z * spreadSlope;
    float extentY = halfHeight + z * spreadSlope;
    float2 uv = float2(
        0.5 + 0.5 * dot(rel, rightUnit) / extentX,
        0.5 + 0.5 * dot(rel, upUnit) / extentY);

    float mx = 1.0 - smoothstep(1.0, 1.2, abs(uv.x - 0.5) * 2.0);
    float my = 1.0 - smoothstep(1.0, 1.2, abs(uv.y - 0.5) * 2.0);
    float mask = mx * my;
    if (mask < 0.0001)
        return (half3)0;

    // A half-SBS 3D frame lights the room from its left eye; a 2D frame from all of it.
    float eyeWidth = _MyScreenSpillFlat > 0.5 ? 1.0 : 0.5;
    float2 sampleUV = float2(saturate(uv.x) * eyeWidth, saturate(uv.y));
    half3 proj = SAMPLE_TEXTURE2D_LOD(_MyScreenSpillTex, MYSCREEN_SPILL_SAMPLER, sampleUV, max(_MyScreenSpillMip, 0.0)).rgb;

    float luma = dot(proj, half3(0.2126, 0.7152, 0.0722));
    if (luma < 0.004)
        return (half3)0;

    float falloff = MyScreenSpillFalloff(dist, radius);
    return (half3)(proj * ndotl * screenFacing * falloff * mask * intensity);
}

#endif
