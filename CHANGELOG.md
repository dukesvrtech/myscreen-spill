# Changelog

## 0.1.0 (2026-11-18)

- `MyScreen/Lit Spill`: URP 17 Lit with MyScreen's spill in its forward pass.
- `MyScreenSpill.hlsl`: the spill as an include for other shaders (`EvaluateMyScreenSpill`). It borrows a sampler
  (`MYSCREEN_SPILL_SAMPLER`) instead of declaring one, and has a 2D/3D flag (`_MyScreenSpillFlat`).
- MyScreen > Spill menu: Add or Update Spill Rig, Select Spill Hub, Convert Selected Materials, Convert Materials in
  Open Scene, Revert Selected Materials to URP Lit.
- The rig: capture camera and texture, target, screen and state markers (BS Object Id), and a hub graph that passes
  the Banter SDK's Visual Scripting check.
