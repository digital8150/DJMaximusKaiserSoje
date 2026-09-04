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

## UI/UX copy rules (frontend)

**이 화면을 보는 사람은 리듬게임을 플레이하는 유저입니다. 기획서를 쓴 사람도, 개발자도 아닙니다.**

가장 흔하고 치명적인 실패는 **지시사항·구현 방식을 그대로 UI 텍스트로 옮겨 적는 것**입니다.

```
지시: "판정은 프레임/deltaTime이 아니라 오디오 DSP 클럭을 기준으로 계산하세요."

❌ 화면 안내: "판정은 오디오 클럭 기반으로 정확하게 계산됩니다."
✅ 화면: 그냥 노트를 치면 판정이 정확하게 나온다. 프레임 드랍에도 밀리지 않는 것은
        플레이해서 체감하는 것이지 읽어서 아는 것이 아니다.
```

**메커니즘은 플레이로 증명되지, 설명으로 증명되지 않습니다.** 잘 만들어진 기능일수록 자기 자랑을 하지 않습니다.

같은 유형의 금지 사례:

- "매치메이킹 시스템이 당신의 실력을 분석하여 이 채보를 추천했습니다" → 그냥 추천 곡을 보여줄 것
- "NoteSpawner가 오브젝트 풀에서 노트를 재사용합니다" → 내부 컴포넌트/클래스 이름을 유저에게 노출하지 말 것
- "이 화면에서는 정확도와 판정 분포를 확인할 수 있습니다" → 화면을 보면 아는 것
- 판정 알고리즘·가중치·스코어 계산식을 툴팁이나 안내문으로 설명하는 것
- 빈 상태에 "여기에 플레이 기록이 표시됩니다" 같은 자리 설명
- `매니저`, `컨트롤러`, `모듈`, `엔진`, `세션`, `카탈로그`처럼 개발자/엔진 어휘를 화면 문구로 쓰는 것

**금지 대상은 단어가 아니라 자랑이다.** 가르는 기준은 그 문장이 **지금 무슨 일이 일어나는지를 알리는가**, 아니면 **결과가 어떻게 만들어졌는지를 자랑하는가**이다.

```
✅ "곡 데이터를 내려받고 있어요. 잠시만 기다려 주세요."
   → 유저가 몇 초를 기다리는 중이다. 왜 멈춰 있는지 알아야 앱을 닫지 않는다.
✅ "네트워크 연결이 끊겼어요. 지금까지 낸 점수는 안전하게 저장됐어요."
   → 뭔가 실패했다는 것과, 무엇은 잃지 않았는지 알아야 한다.
❌ "Addressables 원격 카탈로그를 갱신하여 최신 콘텐츠를 반영했습니다"
   → 읽어도 유저가 할 일이 달라지지 않는다. 그냥 곡이 늘어나 있으면 될 일이다.
```

기다림·실패·소진(다운로드 중, 오프라인, 저장 실패, 콘텐츠 만료)처럼 **유저가 지금 겪고 있는 상태**는 이름을 붙여 알린다. 침묵이 더 나쁘다.

**판별 기준 한 줄: "이 문장을 읽어서 유저의 행동이 달라지는가?"** 아니라면 삭제합니다.

예외는 유저의 판단에 실제로 영향을 주는 정보뿐입니다 — 판정 윈도우(Perfect/Great/Good) 기준, 곡 다운로드 용량, 오프라인 재생 가능 여부, 저장 데이터 삭제 경고 같은 것. 이건 자랑이 아니라 유저가 알아야 움직일 수 있는 조건입니다.

카피는 짧고, 사람 말로, 플레이어의 맥락에서 씁니다. "오늘도 리듬 한 곡 어때요?", "신곡이 도착했어요" 수준이지 기능 설명문이 아닙니다.

## Change workflow

1. Inspect existing modules, tests, packages, and conventions before designing a change.
2. Identify reusable maintained solutions and licensing implications.
3. Define or update tests alongside the module/component.
4. Implement the smallest cohesive change that satisfies the requirement.
5. Run formatting/compilation and relevant EditMode or PlayMode tests.
6. Summarize architectural decisions, package changes, and remaining risks in the handoff.

