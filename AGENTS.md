# AGENTS.md: MyScreen Spill, for AI assistants

This file is for AI coding assistants (Claude, Copilot, Cursor and others) that help a developer use or change this
package. People should read [README.md](README.md). The facts below are a contract with MyScreen's run-time code: keep
them exact.

## What this package is

- A Unity package, `com.dukesvr.myscreen-spill`. It throws the light of a MyScreen screen onto a world's surfaces.
- The worlds are AltspaceVR worlds built with the Banter SDK, running on PC and on Quest (Android, Vulkan).
- **Runtime/Shaders:**
  - `MyScreenSpill.hlsl` is the spill;
  - `MyScreenLitSpill.shader` is `MyScreen/Lit Spill`, URP 17 Lit plus the spill;
  - `LitForwardPassMyScreenSpill.hlsl` is its forward pass, a fork of URP's.
- **Editor:**
  - `MyScreenSpillRig.cs` builds the rig in the open scene;
  - `MyScreenSpillGraph.cs` builds the rig's Visual Scripting graph;
  - `MyScreenSpillMaterials.cs` converts materials.
- Licence: MIT, except `MyScreenLitSpill.shader` and `LitForwardPassMyScreenSpill.hlsl`, which are modified URP code under the Unity Companion License. Keep that note in LICENSE if you add more URP-derived files.
- No runtime C#. **Banter worlds cannot run custom MonoBehaviours.** Behaviour in a world is Visual Scripting graphs
  (limited to the SDK's allowed members) and the world page's JavaScript.

## The run-time contract (do not change without changing MyScreen)

MyScreen's gateway script (`altvr-props/src/myscreen/spill.ts` in the MyScreen repo) finds these scene objects **by exact
name** through their BS Object Id:

| Object | MyScreen writes | Meaning |
|---|---|---|
| `MyScreenSpillTarget` | adds a child plane (1.52 x 0.86 m, Unlit/Diffuse) showing the TV's picture (`asset_video_<id>`) | what the rig's camera films |
| `MyScreenSpillScreen` (scene root) | local position = screen centre; local euler = (0, yaw, 0); local scale = (width m, height m, 1) | where the light comes from; **front is -Z** |
| `MyScreenSpillScreen1` ... `5` (scene roots, 0.2) | the same, one per further screen; local scale (0, 0, 0) when the slot has no screen | more screens; scaled to nothing = off |
| `MyScreenSpillState` (child of `MyScreenSpill`) | local position = (flat, strength, 0) | flat 1 = 2D picture, 0 = half-SBS 3D; strength 1 = normal |

MyScreen puts a marker on each of its screens in the world (the TV and its wall screens), up to 6, the biggest first, and
moves them again when one moves or is resized. A rig with only `MyScreenSpillScreen` (0.1) gets the biggest screen.
Overlapping light is MAX-blended in the shader, not summed. In the editor, the developer's placement of `MyScreenSpillScreen` only previews the light.

The hub graph (`MyScreenSpill/MyScreenSpillHub`) runs at Start and every 10 frames. It reads the markers and its
Variables (`Screen`, `Screen1` ... `Screen5`, `State`, `Capture`, `Radius`, `Intensity`, `Mip`) and sets these shader globals:

```
_MyScreenSpillTex        Texture2D  the capture (MyScreenSpillCapture.renderTexture, 480x270, mips)
_MyScreenSpillCenter     float4     xyz = Screen.position
_MyScreenSpillRight      float4     xyz = Screen.right,  w = |lossyScale.x| / 2
_MyScreenSpillUp         float4     xyz = Screen.up,     w = |lossyScale.y| / 2
_MyScreenSpillNormal     float4     xyz = -Screen.forward
_MyScreenSpillCenter1..5, _MyScreenSpillRight1..5, _MyScreenSpillUp1..5, _MyScreenSpillNormal1..5
                                    the same from Screen1 ... Screen5 (0.2; half sizes 0 = slot off)
_MyScreenSpillRadius     float      Radius
_MyScreenSpillIntensity  float      Intensity * State.localPosition.y
_MyScreenSpillMip        float      Mip
_MyScreenSpillFlat       float      State.localPosition.x
```

## Invariants

- **Names:** keep the object names, the global names and the include path
  (`Packages/com.dukesvr.myscreen-spill/Runtime/Shaders/MyScreenSpill.hlsl`).
- **No new samplers in the spill.** It samples with `MYSCREEN_SPILL_SAMPLER`, which defaults to `sampler_BaseMap`. URP
  Lit can already use 16 sampler slots, and a 17th fails on D3D11, GLES3 and Vulkan (Quest).
- **The graph:** use only members the Banter SDK allows:
  - `Shader.SetGlobalTexture`, `SetGlobalFloat`, `SetGlobalVector`;
  - `GameObject.transform`;
  - `Transform` `position`, `right`, `up`, `forward`, `lossyScale`, `localPosition`;
  - `Vector3` and `Vector4` maths;
  - `Time.frameCount`;
  - Start and Update.

  Check with the SDK: `BS.SDKEditor.ValidateVisualScripting.CheckVsNodes(IEnumerable<GameObject>, bool)` must return
  true for the hub.
- **The graph is generated.** Change `MyScreenSpillGraph.cs`, then run **MyScreen > Spill > Add or Update Spill Rig**,
  which replaces the embedded graph. Hand edits in the graph window are lost.
- **Null checks:** in editor code, never use `??` or `?.` on Unity objects, because a missing component is Unity-null
  but not C#-null. Use an explicit `!= null` check.
- **Dependencies:** keep them minimums (URP 17.0.3, Visual Scripting 1.9.0). A higher minimum would upgrade the
  developer's own project.
- **`MyScreenLitSpill.shader` stays URP Lit:** the same properties, the same `CustomEditor`, the same passes. Only the
  forward pass adds the spill. This is what makes material conversion a shader swap.

## Common tasks

**Set up a world** (the developer's scene):
1. Run `MyScreen > Spill > Add or Update Spill Rig` (or `MyScreen.Spill.Editor.MyScreenSpillRig.Build(scene)`).
2. Place `MyScreenSpillScreen` on the screen: its centre, front -Z, scale = (width, height, 1).
3. Select the surfaces in front of the screen and run `MyScreen > Spill > Convert Selected Materials`. Only URP Lit
   converts; tell the developer which materials were skipped and why.
4. Tune `Radius`, `Intensity` and `Mip` on `MyScreenSpillHub`'s Variables.

**Add the spill to another shader:** include `MyScreenSpill.hlsl` after the shader has its texture samplers. Define
`MYSCREEN_SPILL_SAMPLER` if it has no `sampler_BaseMap`. Add `EvaluateMyScreenSpill(positionWS, normalWS)` to the
irradiance (URP: `inputData.bakedGI`; unlit: `albedo * spill`).

**Test without MyScreen:** in Play Mode, put any textured quad (1.52 x 0.86) under `MyScreenSpillTarget`, then check:
- the capture (`Shader.GetGlobalTexture("_MyScreenSpillTex")`) shows it;
- spill surfaces in front of the marker show its colours;
- `MyScreenSpillState`'s x switches between left half (0) and whole frame (1).

## Do not

- Add MonoBehaviours, runtime scripts or `[ExecuteAlways]` components to the rig: Banter worlds reject them.
- Set the spill's globals per material, or add material properties for them. They are globals so that one hub lights
  every material.
- Rename or move the markers in a way that changes their names, or remove their BS Object Id.
- Raise the capture's resolution without measuring Quest frame time. 480 x 270 was chosen after the full-size capture
  cost Quest 3 frame time.
