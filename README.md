# MyScreen Spill

The light of a MyScreen screen thrown onto your world, the way a cinema screen lights its seats. Bright scenes wash the
floor, walls and seats in front of the screen with the picture's colours; dark scenes go dark.

It is a URP Lit shader with the spill built in, plus a small rig that MyScreen drives at run time. You add the rig,
convert your materials, and upload. MyScreen does the rest: it feeds the picture, and it follows the biggest MyScreen
screen in your world as people move or resize it.

> For AltspaceVR worlds (Banter SDK) that use the MyScreen TV. AI assistants: read [AGENTS.md](AGENTS.md).

## Requirements

- Unity 6 (6000.0 or later), Universal Render Pipeline 17, Visual Scripting (the Banter SDK brings it).
- The Banter SDK in the project (the rig's markers carry a BS Object Id so MyScreen can find them).
- A MyScreen TV in the world (`<bs-snippet key="myscreen">`). Without one, the spill stays dark.

## Install

Unity: **Window > Package Manager > + > Add package from git URL…**

```
https://github.com/dukesvrtech/myscreen-spill.git#v0.1.1
```

## Set up a world (about five minutes)

1. **Add the rig:** **MyScreen > Spill > Add or Update Spill Rig.** It adds two objects to the open scene:
   - `MyScreenSpill`, far below the world at y -1000, out of sight;
   - `MyScreenSpillScreen`, the marker for where the light comes from.
2. **Place the marker on your screen.** Put `MyScreenSpillScreen` where your MyScreen screen will hang:
   - **Position:** the screen's centre.
   - **Rotation:** its **front faces -Z**. The Scene view draws an outline and a "front" arrow.
   - **Scale:** X is the screen's width and Y its height, in metres. For example `(6, 3.375, 1)` is a 6 m 16:9 screen.

   At run time MyScreen moves the marker onto the biggest MyScreen screen in the world, so this placement only shows the
   light in the editor.
3. **Convert your materials.** Select the objects (or materials) that should catch the light, such as the floor, walls,
   seats and ceiling, then run **MyScreen > Spill > Convert Selected Materials.**
   - Only **URP Lit** materials convert. `MyScreen/Lit Spill` has Lit's properties and Inspector, so textures, colours
     and settings stay. Other shaders are listed in the Console and left alone.
   - **Convert Materials in Open Scene** does every URP Lit material in the scene at once.
   - **Revert Selected Materials to URP Lit** undoes it.
4. **Tune it (optional):** **MyScreen > Spill > Select Spill Hub**, then edit its Variables:

   | Variable | Default | What it does |
   |---|---|---|
   | `Radius` | 18 | metres: no light beyond this distance from the screen |
   | `Intensity` | 1.5 | strength; 0 switches the spill off |
   | `Mip` | 1.6 | the smear: higher is softer (and cheaper); 0 is a sharp picture |

5. **Upload the world** as usual.

Running **Add or Update Spill Rig** again updates the rig to this package's version. It keeps your marker's place and your
tuning.

## How it works

```
 MyScreen (gateway script, at run time)
   ├─ puts the TV's picture on a plane under ── MyScreenSpillTarget
   ├─ moves ─────────────────────────────────── MyScreenSpillScreen, MyScreenSpillScreen1 ... 5
   │                                            (centre, yaw, width x height: one per screen, up to 6)
   └─ sets ──────────────────────────────────── MyScreenSpillState   (x: 1 = 2D picture, 0 = half-SBS 3D; y: strength)

 MyScreenSpill rig (in your scene)
   MyScreenSpillCamera  films the target into MyScreenSpillCapture (480 x 270, with mips)
   MyScreenSpillHub     a Visual Scripting graph: at Start and every 10 frames, sets the shader globals
                        from the markers and its Variables

 MyScreen/Lit Spill materials   read the globals and add the picture's light to the surface
```

The light falls only in front of the screen and only on surfaces turned towards it. It widens with distance, so the floor
and ceiling get picture detail, and it fades out smoothly at `Radius`. A 3D (half-SBS) film lights the room from its
left-eye picture; a 2D one from its whole frame.

## For shader developers

Any shader can take the spill. Include `MyScreenSpill.hlsl` and add `EvaluateMyScreenSpill` to the surface's irradiance:

```hlsl
#include "Packages/com.dukesvr.myscreen-spill/Runtime/Shaders/MyScreenSpill.hlsl"
// ... in the fragment, with world-space position and normal:
half3 spill = EvaluateMyScreenSpill(positionWS, normalWS);
inputData.bakedGI += spill;          // URP: albedo and occlusion still apply
// unlit: color.rgb += albedo * spill;
```

**Sampler:** the spill declares no sampler of its own. It borrows `sampler_BaseMap` by default, because URP Lit can
already use all 16 sampler slots in some variants, and a 17th fails to compile on D3D11, GLES3 and Vulkan (Quest). In a
shader without `sampler_BaseMap`, define one you already have before the include:

```hlsl
#define MYSCREEN_SPILL_SAMPLER sampler_MainTex
#include "Packages/com.dukesvr.myscreen-spill/Runtime/Shaders/MyScreenSpill.hlsl"
```

**Globals** (set by the hub; do not set them per material):

| Global | Type | Meaning |
|---|---|---|
| `_MyScreenSpillTex` | Texture2D | the captured picture, with mips |
| `_MyScreenSpillCenter` | float4 | xyz: the screen's centre, world space |
| `_MyScreenSpillNormal` | float4 | xyz: the screen's facing (towards the audience), unit |
| `_MyScreenSpillRight` | float4 | xyz: the screen's right, unit; w: half its width (m) |
| `_MyScreenSpillUp` | float4 | xyz: the screen's up, unit; w: half its height (m) |
| `_MyScreenSpillRadius` | float | metres: no light beyond |
| `_MyScreenSpillIntensity` | float | strength (Intensity x the state's y); 0 is off |
| `_MyScreenSpillMip` | float | the mip level sampled |
| `_MyScreenSpillFlat` | float | 1: 2D, the whole frame; 0: half-SBS 3D, the left half |

`MYSCREEN_SPILL_SPREAD` (default 4.0) sets how fast the throw widens with distance; define it before the include to
change it.

`MyScreenLitSpill.shader` is URP 17's Lit with one change: its forward pass (`LitForwardPassMyScreenSpill.hlsl`, a fork of
URP's `LitForwardPass.hlsl`) adds the spill to `inputData.bakedGI`. The other passes are URP's own.

## Performance (Quest)

- One extra camera renders a single plane into a 480 x 270 texture every frame, and its mips are generated.
- Per pixel on spill materials: a few dot products and one texture sample at a low mip, skipped early behind the screen,
  outside `Radius`, on surfaces turned away, and in dark scenes.
- Convert only what is in front of the screen. Surfaces behind it or far away gain nothing.

## Troubleshooting

| What you see | Check |
|---|---|
| No light at all | A film must be playing, and dark scenes give little light. The world's page log shows "this world has a spill rig: MyScreen lights it". |
| "the spill rig is incomplete" in the log | Run **Add or Update Spill Rig** again: `MyScreenSpillTarget`, `MyScreenSpillScreen` and `MyScreenSpillState` each need their BS Object Id. |
| Some surfaces stay dark | They are not on `MyScreen/Lit Spill` (only URP Lit converts), or they face away from the screen, or they are beyond `Radius`. |
| The light comes from the wrong place | At run time it follows the **biggest** MyScreen screen in the world (a wall screen beats the TV). The marker's editor placement is only for previewing. |
| The light looks doubled or squashed | The 3D/2D flag comes from the film; MyScreen sets it. |
| Shader errors about sampler registers | You defined `MYSCREEN_SPILL_SAMPLER` to a new sampler. Borrow one the shader already declares. |

## Licence

MIT, see [LICENSE](LICENSE), with one exception: `MyScreenLitSpill.shader` and `LitForwardPassMyScreenSpill.hlsl` are
modified copies of Unity's URP Lit shader and stay under the Unity Companion License (use with Unity only). The spill
itself, `MyScreenSpill.hlsl`, and the editor tools are MIT.
