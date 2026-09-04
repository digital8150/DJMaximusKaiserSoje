# Game structure promotion

Promotes the single-scene prototype (`Assets/RhythmPrototype`, one 1000-line `MonoBehaviour` that
builds every screen at runtime) into a scene-per-screen game structure with authored UGUI prefabs.

## Scenes

| Scene | Role | Screen music |
| --- | --- | --- |
| `Assets/Scenes/Boot.unity` | Composition root. Builds services, warms Addressables, hands off to Title. No visible UI beyond a loading state. | none |
| `Assets/Scenes/Title.unity` | Title / attract screen. Any key continues. | `theme.title` |
| `Assets/Scenes/SongSelect.unity` | Song list, difficulty pick, play settings, personal best. | `theme.song-select`, replaced by the highlighted song's preview after a 0.5 s dwell |
| `Assets/Scenes/Gameplay.unity` | Playfield, HUD, judgement feedback, health, progress. | the chart's own audio |
| `Assets/Scenes/Result.unity` | Score breakdown, rank, records, fast/slow. | `theme.result` |

`Boot` is the only scene in the build settings that must load first; every other screen is loaded
single (not additive) by the flow service, which lives on the bootstrap object and survives loads.

## Assemblies

| Assembly | Path | Depends on | Owner |
| --- | --- | --- | --- |
| `DJMaximusKaiserSoje.Core` | `Assets/Game/Runtime/Core` | nothing (`noEngineReferences`) | contracts: Claude · implementations: Codex |
| `DJMaximusKaiserSoje.Content` | `Assets/Game/Runtime/Content` | Core, Addressables | Codex |
| `DJMaximusKaiserSoje.Gameplay` | `Assets/Game/Runtime/Gameplay` | Core, Content, InputSystem | Codex |
| `DJMaximusKaiserSoje.App` | `Assets/Game/Runtime/App` | Core, Content, Gameplay | Codex |
| `DJMaximusKaiserSoje.Presentation` | `Assets/Game/Runtime/Presentation` | Core, ugui, TextMeshPro | Claude |
| `DJMaximusKaiserSoje.Editor` | `Assets/Game/Editor` | all runtime, Addressables.Editor | Claude |
| `DJMaximusKaiserSoje.Tests.EditMode` | `Assets/Game/Tests/EditMode` | all runtime | Codex (Presentation tests: Claude) |

`Core` compiles without UnityEngine on purpose: it is the deterministic half of the game and the
boundary is enforced by the compiler, not by convention.

## Ownership boundaries

Two agents work this repository in parallel. Directory ownership is the conflict rule.

- **Claude (frontend)** — `Runtime/Presentation/**`, `Assets/Game/UI/**`, `Assets/Game/Editor/**`,
  `Assets/Scenes/*.unity`, and the contract files listed below.
- **Codex backend worker** — `Runtime/Core` implementations, `Runtime/Content/**`,
  `Runtime/Gameplay/**`, `Runtime/App/**`, `Assets/Game/Tests/**`.
- **Codex art worker** — `Assets/Game/UI/Art/Generated/**` and `tools/art/**` only.

Contracts are **append-only**. A worker that needs a different shape adds a new member and says so
in its handoff instead of editing an existing signature, so the other side never fails to compile
against a moved target.

Only Claude runs the Unity editor (batch mode). Nobody hand-edits `.unity`, `.prefab`, or `.meta`.

## Contract surface

All of it lives in `Assets/Game/Runtime/Core/Contracts` and is engine-free.

- `ISongLibrary` — the catalog the select screen lists (`SongSummary`, `ChartSummary`).
- `IRecordStore` — per-chart personal bests (`ChartRecord`), and result submission.
- `IPlayerProfile` — display name, tag, level, exp progress.
- `IPlayPreferences` — scroll speed, judgement offset, play style; persisted.
- `IGameFlow` — screen transitions and the play/result handoff (`PlayRequest`).
- `IPlaySession` — one run in progress; the gameplay HUD binds only to this.
- `IMusicDirector` — screen themes and song previews, including the 0.5 s dwell debounce.

## Song preview behaviour

The select screen calls `IMusicDirector.RequestSongPreview(songId)` on every highlight change and
never times anything itself. The director holds the request for `PreviewDwell` (0.5 s), and only
then crossfades from the screen theme into the chart's preview point (osu `PreviewTime`, or 40 % of
the way in when the chart declares none). Moving off the song before the dwell elapses cancels the
request; leaving the screen returns to the theme. The debounce is testable with an injected clock.

## Art pipeline

Generated art is produced against a pure chroma background (`#00ff00`, or `#ff00ff` when the subject
is itself green) and then keyed to straight alpha with a despill pass, because the generator cannot
emit transparency. Raw generations stay out of `Assets/`; only keyed PNGs land in
`Assets/Game/UI/Art/Generated`. UI chrome that must stay crisp — 9-slice panels, gauges, note skins,
dividers — is drawn deterministically by script instead of generated.

Style: neon-cute with an original mascot. Nothing from the reference screenshots is reproduced —
no logos, mascot likenesses, or brand strings.

## Rebuilding the front end

The screens are described in editor code and stamped into scenes by Unity, so nothing about them is
hand-edited YAML. From a clean clone, with the editor closed:

```sh
tools/unity.sh DJMaximusKaiserSoje.Editor.GamePipeline.PrepareAssets   # TMP, fonts, chrome sprites
tools/unity.sh DJMaximusKaiserSoje.Editor.GamePipeline.BuildScreens    # prefabs, scenes, build list
tools/unity.sh DJMaximusKaiserSoje.Editor.ContentPipeline.Migrate      # song content + Addressables keys
```

All three are also on the `Tools > DJ Maximus` menu inside the editor. They are idempotent:
re-running overwrites the generated sprites and rebuilds every scene from the builder scripts, so a
layout change is a code change, not a manual re-drag.

`tools/unity.sh DJMaximusKaiserSoje.Editor.ScreenshotCapture.CaptureAll` renders each screen to
`artifacts/screens` so a layout can be reviewed without launching the game.

## Checks

```sh
tools/csccheck.sh                                     # every assembly, Roslyn, ~20s, no Unity
Unity.exe -batchmode -runTests -testPlatform EditMode  # domain, parsing, scoring, persistence
Unity.exe -batchmode -runTests -testPlatform PlayMode  # boot → select → play, with screenshots
```

`csccheck.sh` compiles against Unity's own reference assemblies while the editor is busy or closed;
Unity stays the authority. The PlayMode smoke tests walk the real scenes and leave
`Runtime-SongSelect.png` and `Runtime-Gameplay.png` behind, which is what catches wiring that
compiles but does not run.

## Fonts

Pretendard (SIL OFL 1.1) under `Assets/Game/UI/Fonts`, with the license file kept beside it. TMP font
assets use a dynamic atlas so Korean glyphs are rasterized on demand rather than pre-baked.
