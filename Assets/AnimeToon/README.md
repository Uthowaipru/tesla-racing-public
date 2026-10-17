# Anime Toon (URP, Unity 6)

Universal anime-style rendering tools. Contains no game-specific code, so the whole
folder can be copied between projects. Look/mood lives in presets and material settings.

## Install
1. Delete any old loose copies of these shaders/scripts first (two shaders with the same
   name conflict). To keep existing material references, overwrite the old files in place
   instead of deleting them.
2. Copy this `AnimeToon` folder into your project's `Assets/`.

## Contents
```
AnimeToon/
  Shaders/
    Character/AnimeCharacterToon.shader     Anime/CharacterToon
    Environment/AnimeEnvironmentToon.shader Anime/EnvironmentToon
    Environment/AnimeGlass.shader           Anime/Glass
    Environment/AnimeWater.shader           Anime/Water
    Sky/AnimeSky.shader                     Anime/Sky
  Runtime/
    AnimeStylePreset.cs                     one mood (Day / Dusk / Night)
    AnimeStyleController.cs                 blends presets, drives sun/ambient/fog + shader globals
  Editor/
    AnimeSmoothNormalBaker.cs               bakes outline normals for models named *_anime
    AnimeStylePresetCreator.cs              Tools > Anime Toon > Create Default Style Presets
```

## Quick start
- **Character:** name the model file `Something_anime.fbx` (Normals: Import/Calculate,
  Tangents: Calculate Mikktspace). Assign `Anime/CharacterToon`.
  Vertex color R = outline width, G = face shading mask. Mask texture: R spec, G ramp softness, B AO.
  Single-face meshes: set Cull to Off, or Outline Width to 0.
- **Buildings / props:** `Anime/EnvironmentToon`. Enable Facade for procedural windows.
  Alpha Clipping + Cull Off for leaf cards. Leave Vertex AO Strength at 0 unless the mesh has baked AO.
- **Glass:** `Anime/Glass`. Reflects the skybox / reflection probe (Generate Lighting if it looks black).
- **Water:** `Anime/Water`. Enable Depth Texture in the URP asset.
- **Sky:** Tools > Anime Toon > Create Sky Material (sets it as the skybox). With an
  `AnimeStyleController` in the scene the sky colors, clouds, glow and stars follow the presets.
- **Post processing:** Tools > Anime Toon > Create Anime Volume Profile (bloom, neutral tonemapping,
  saturation/contrast, split toning, vignette; assigned to the controller, which then blends it per preset).
  Turn on Post Processing on the camera. Disable any other Volume that also overrides Bloom/Tonemapping.
- **Mood:** add an empty GameObject with `AnimeStyleController`, assign your Directional Light,
  run Tools > Anime Toon > Create Default Style Presets, then scrub Time Of Day.
  If you made presets with an older version, run Tools > Anime Toon > Reset Default Style Presets (Overwrite)
  so they get the new sky values.

## URP requirements
- Additional Lights: Per Pixel (point/spot lights on toon materials).
- Depth Texture: on (water).
- Realtime lights only: no baked lightmap / baked GI support yet.

## Notes
- Global shader values (`_Anime_*`) default to 0, which means no effect.
- Characters use back-face outlines; buildings have none (hard edges would crack).
