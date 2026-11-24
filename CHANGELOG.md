# Changelog

## 0.2.0 (2026-11-24)

- Up to 6 screens light the room: the TV and its wall screens, all with the same picture. Where their light overlaps,
  the brightest counts (a MAX blend), so screens showing the same film do not over-brighten the room.
- The rig has `MyScreenSpillScreen1` ... `MyScreenSpillScreen5` beside `MyScreenSpillScreen` (run **Add or Update
  Spill Rig** again to get them). New markers start scaled to nothing, which is off. Slots 1-5 set
  `_MyScreenSpillCenter1` ... `_MyScreenSpillNormal5`; slot 0 keeps 0.1's globals, so a 0.1 rig still lights from one
  screen (MyScreen gives it the biggest).
- Still one capture and one texture: the extra cost is the per-pixel throw of each lit slot, no more samplers.

## 0.1.1 (2026-11-18)

- Converting materials changes only their shader: a material on its shader's own render queue stays on it (0.1.0
  fixed it at the queue's number).

## 0.1.0 (2026-11-18)

- `MyScreen/Lit Spill`: URP 17 Lit with MyScreen's spill in its forward pass.
- `MyScreenSpill.hlsl`: the spill as an include for other shaders (`EvaluateMyScreenSpill`). It borrows a sampler
  (`MYSCREEN_SPILL_SAMPLER`) instead of declaring one, and has a 2D/3D flag (`_MyScreenSpillFlat`).
- MyScreen > Spill menu: Add or Update Spill Rig, Select Spill Hub, Convert Selected Materials, Convert Materials in
  Open Scene, Revert Selected Materials to URP Lit.
- The rig: capture camera and texture, target, screen and state markers (BS Object Id), and a hub graph that passes
  the Banter SDK's Visual Scripting check.
