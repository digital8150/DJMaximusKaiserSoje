# Backend handoff — Codex worker

Read `AGENTS.md` and `docs/game-structure.md` first; both bind this work.

You own the runtime behind the contracts. The UI is being built in parallel against those same
contracts, so the contract files are the one thing you must not change.

## Boundaries

**Yours**
- `Assets/Game/Runtime/Content/**`
- `Assets/Game/Runtime/Gameplay/**`
- `Assets/Game/Runtime/App/**`
- `Assets/Game/Runtime/Core/**` — implementations and pure helpers only
- `Assets/Game/Tests/EditMode/**`
- `Assets/Game/Editor/Content/**` — importer and Addressables configuration only
- `Assets/Game/Content/**` — the song catalog document

**Not yours, do not open for writing**
- `Assets/Game/Runtime/Core/Contracts/**` — frozen. Need a different shape? Add a new member and
  write it down in your final summary rather than changing an existing signature.
- `Assets/Game/Runtime/Presentation/**`, `Assets/Game/UI/**`, `Assets/Game/Editor/Build/**`,
  `Assets/Game/Editor/Art/**`, `Assets/Scenes/**`
- Any `.unity`, `.prefab`, or `.meta` file, anywhere.

Do not run Unity, do not run git commit, do not push. Another agent drives the editor and will
compile and test your code; leave the working tree dirty.

## What to build

### 1. Content (`DJMaximusKaiserSoje.Content`, references Core + Addressables)

- Port `Assets/RhythmPrototype/Runtime/OsuManiaBeatmapParser.cs` onto the new `Beatmap` /
  `BeatmapHeader` model. It must now also read `PreviewTime` from `[General]` and a representative
  BPM from the first uninherited timing point. Keep the parser's DTOs away from the domain model.
- Song catalog schema v2: per-chart `tier` (`DifficultyTier`), `level`, `noteCount`; per-song
  `bpm`, `category`. Accept v1 documents by inferring sensible defaults, and cover the migration
  with a test — do not silently drop unknown versions.
- `ISongLibrary` implementation built from the catalog. Chart ids must be stable and derivable
  (`<songId>.<tier>` is fine) because records are keyed by them.
- A loader for chart text, audio, jacket, and video through Addressables logical keys, surfacing
  failures as typed results rather than exceptions in the middle of a coroutine.
- Theme music addresses: `theme.title`, `theme.song-select`, `theme.result`, pointing at
  `Assets/Game/Content/Music/Theme-*.mp3`.

### 2. Core implementations (pure, no UnityEngine)

- `ScoreAccumulator` — feeds on judgements and yields `RunScore`. Define the score and rating
  formulas here, with tests that pin the boundaries (all-perfect, all-miss, empty chart).
- `HealthRules` — how each grade moves `HealthState`, and when a run fails.
- `SectionTimeline` — splits a chart into named sections over song time and answers "which section
  is time T in". Keep the naming plain and deterministic.
- `PreviewPointResolver` — a chart's preview start: its declared `PreviewTime`, else
  `MusicTiming.PreviewFallbackPosition01` of its length.

### 3. Gameplay (`DJMaximusKaiserSoje.Gameplay`, references Core + Content + InputSystem)

- `PlaySession : IPlaySession`. Port the run logic out of
  `Assets/RhythmPrototype/Runtime/RhythmGamePrototype.cs`: DSP-clock timing, scheduled audio start,
  input timestamps translated into the DSP domain, tap and hold judgement, misses, combo, health,
  sections, and the `PlayResult` it finishes with. Keep every bit of UI construction out of it —
  the class must not touch `UnityEngine.UI`, build GameObjects, or know a Canvas exists.
- Lane input through the Input System, driven by `LaneLayout`, raising `LanePressed`/`LaneReleased`.
- Judgement is never derived from frame count or accumulated `deltaTime`.

### 4. App (`DJMaximusKaiserSoje.App`, references Core + Content + Gameplay)

- `GameBootstrap` — the composition root on the Boot scene, surviving loads. Builds `GameServices`,
  warms Addressables, then hands off to the title screen.
- `SceneGameFlow : IGameFlow` — loads `Title`, `SongSelect`, `Gameplay`, `Result` by name, finds the
  loaded scene's `IScreenView`, and binds it (`BindSession` / `ShowResult` for the two that need
  more). Scene names live in one place.
- `MusicDirector : IMusicDirector` — two audio sources and a crossfade; the 0.5 s preview dwell is
  debounced here against an injected clock so it can be tested without playing anything. Leaving a
  screen returns to the theme.
- `PlayerPrefsPlayPreferences : IPlayPreferences`, `JsonRecordStore : IRecordStore` (under
  `Application.persistentDataPath`, tolerant of a missing or corrupt file), `LocalPlayerProfile`.
- A factory that turns a `PlayRequest` into a loaded, ready `IPlaySession`.

### 5. Retire the prototype

Once the above covers it, delete `Assets/RhythmPrototype/Runtime`, `Editor`, `Tests`, and
`README.md`, and move `Assets/RhythmPrototype/Content` and `Art` to `Assets/Game/Content` and
`Assets/Game/UI/Art/Prototype`. Leave the `.meta` files alone — say in your summary which paths
moved and the editor-side agent will perform the move through Unity so GUIDs survive. **Do not move
or delete asset files yourself; list them.**

### 6. Tests

EditMode tests in `Assets/Game/Tests/EditMode` with its own asmdef, covering at minimum: parser
happy path and malformed input, catalog v1→v2 migration, score and accuracy boundaries, health
failure, section lookup at boundaries, rank thresholds, preview-point fallback, the preview dwell
debounce, and record merging (a worse run must not overwrite a better one).

## Reporting

Finish with a short summary: what you built, which contract members you wished were different, the
list of asset paths that need moving, and anything you left undone.
