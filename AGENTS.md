# DJMaximusKaiserSoje Agent Guide

## Project brief

DJ막시무스 카이저 쏘제 is an extensible, single-player keyboard rhythm game prototype built with Unity 6.6 and Universal 2D. Gameplay may use perspective-styled lanes while retaining a primarily 2D presentation. The internal beatmap representation is based on the osu! beatmap format. Song content must be deliverable through Addressables remote catalogs without requiring a new player build.

## Non-negotiable engineering rules

- Do not reinvent the wheel. Before implementing infrastructure, parsers, serialization, timing, pooling, reactive/event systems, or editor tooling, evaluate maintained and license-compatible Unity/C# packages and existing project components. Record why a custom implementation is necessary when no suitable option exists.
- Apply object-oriented design and SOLID principles pragmatically. Keep responsibilities narrow, program against abstractions at boundaries, and prefer composition over inheritance.
- Write clean, intention-revealing code. Use clear names, small cohesive methods, explicit dependencies, and guard clauses. Avoid hidden global state, service locators, and needless abstractions.
- Every new or materially changed module/component requires unit tests. Prefer EditMode tests for deterministic domain logic and PlayMode tests only for behavior that needs the Unity runtime.
- A change is incomplete when its relevant tests do not compile or pass.

## Architecture boundaries

- Keep rhythm-domain logic (beatmaps, timing, scoring, judgments, modifiers) in plain C# assemblies with minimal UnityEngine coupling so it remains deterministic and easy to test.
- Isolate osu! file parsing and mapping from the canonical internal beatmap model. Do not let parser-specific DTOs leak into gameplay systems.
- Treat audio/DSP time as the authoritative gameplay clock. Do not base note judgment on frame count or accumulated `deltaTime`.
- Keep presentation, input, audio scheduling, content delivery, persistence, and domain rules in separate modules with explicit interfaces.
- Use assembly definition files for production modules and matching test assemblies as the codebase grows.

## Addressables and song content

- Song metadata, beatmaps, cover art, preview audio, and playable audio are content, not hard-coded player-build dependencies.
- Access remote content through stable logical keys or labels; never depend on catalog-generated paths or hashes.
- Version content schemas explicitly and maintain backward compatibility or a migration path.
- Keep a small local fallback/bootstrap catalog so startup and error recovery remain usable offline.
- Test catalog update, download failure, cache invalidation, partial content, and incompatible schema scenarios before release.
- Do not commit built Addressables output or downloaded content caches unless a documented release workflow explicitly requires it.

## Testing expectations

- Follow Arrange-Act-Assert and keep tests deterministic, isolated, and fast.
- Cover happy paths, boundary timing windows, malformed beatmaps, cancellation/error paths, and regression cases.
- Abstract clocks, file access, network access, and random sources behind injectable interfaces.
- Name tests by observable behavior, for example `Parse_WhenTimingPointIsMalformed_ReturnsDiagnostic`.
- Run the smallest relevant test suite while iterating and the full EditMode suite before merging.

## Unity asset discipline

- Never hand-edit `.unity`, `.prefab`, or other Unity-serialized YAML when the Unity Editor or Pipeline command can perform the change safely.
- Commit every Unity asset together with its `.meta` file. Never regenerate GUIDs casually.
- Keep `Library`, `Temp`, `Logs`, `obj`, build output, user settings, and Addressables build/cache output out of Git.
- Store large binary audio, artwork, video, and source assets through Git LFS.
- Avoid introducing packages that duplicate template-provided capabilities. Add packages through Unity Package Manager APIs and document their purpose.

## Change workflow

1. Inspect existing modules, tests, packages, and conventions before designing a change.
2. Identify reusable maintained solutions and licensing implications.
3. Define or update tests alongside the module/component.
4. Implement the smallest cohesive change that satisfies the requirement.
5. Run formatting/compilation and relevant EditMode or PlayMode tests.
6. Summarize architectural decisions, package changes, and remaining risks in the handoff.

