# Hit flipbook sheets

Six white-on-alpha flipbooks that VfxDirector plays for a hit, wired through
`Assets/Resources/Content/vfx.json` (`types`, field `sheet`). White so the attacker's aspect
accent tints them; nothing here is element-literal.

Source art: **Vefects, "Free URP Flipbook VFX Bundle Lite"** (Unity Asset Store), used under the
Standard Unity Asset Store EULA. Only the textures below were taken, and only as processed
here. None of the pack's prefabs, materials, shaders or scripts are in this project.

| Sheet | Grid | Frames | Cell | From |
|---|---|---|---|---|
| `impact_spikes` | 3x2 | 6 | 260 | Hit_09 (blank trailing row dropped) |
| `slash_sweep` | 3x3 | 9 | 260 | Hit_03 |
| `bolt_crackle` | 3x3 | 9 | 260 | Electric_Impact_01 |
| `burst_puff` | 4x4 | 16 | 260 | Explosion_02 |
| `heal_ring` | 4x2 | 8 | 260 | Radial_Burst_02 (also used for `buff`) |
| `shadow_gather` | 4x2 | 8 | 260 | Radial_Burst_02, frame order reversed |

Processing: the pack's `_Alpha` textures are RGB masks with no alpha channel, so the shape
channel (R) became alpha and RGB was set to white. Each frame was resampled from 2048 px sheets
to 260 px cells (260 is a multiple of 4 and 5, so BC7 and ASTC 5x5 blocks never straddle two
frames) and given a 6 px alpha feather at the cell edge so bilinear sampling cannot pull in a
neighbouring frame. Import settings come from `Tools/texture-import-policy.py` (group
"Hit flipbook sheets").
