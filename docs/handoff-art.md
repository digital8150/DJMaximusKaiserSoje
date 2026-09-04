# Art handoff — Codex worker

Read `AGENTS.md` and `docs/game-structure.md` first.

You produce the illustrated art for a keyboard rhythm game's three screens. UI chrome — panels,
frames, gauges, note skins — is drawn by script elsewhere and is not your job. You make the things a
generator is actually good at: a mascot, backgrounds, and ornaments.

## Style

Neon-cute. Pastel neon on deep indigo, cyan and magenta accents, soft glow, clean vector-ish
lineart, a bright cheerful mascot. Original designs only — nothing from any existing rhythm game:
no existing mascot likenesses, logos, or brand strings.

The mascot is a recurring character: a small energetic DJ, headphones, oversized jacket, teal and
magenta hair accents. Keep her consistent across all three poses — same face, same outfit, same
palette. Describe her identically in every prompt.

## Chroma workflow

The generator cannot emit transparency, so every cut-out asset is generated against a **pure
uniform chroma background** and keyed afterwards:

- Use `#00ff00` by default. Use `#ff00ff` when the subject itself contains a lot of green.
- The prompt must ask for a flat, uniform, solid background of that exact colour, with no gradient,
  no shadow cast onto it, no vignette, and no glow bleeding into it.
- Then key it: build the alpha from chroma distance with a soft edge (not a hard binary threshold),
  **despill** the remaining chroma fringe on semi-transparent edge pixels, and write straight-alpha
  PNG. A visible green rim on a dark background is a failed asset — check the result and redo it.
- Autocrop to the subject's bounding box plus a few pixels of padding.

Write the keying script under `tools/art/` and keep it re-runnable. There is no PIL and no image
library installed; Windows PowerShell with `System.Drawing` (`Add-Type -AssemblyName System.Drawing`,
`LockBits` over the pixel buffer) works out of the box and is what you should use. Do not install
packages.

Raw generations stay out of `Assets/`. Keep them under `tools/art/raw/` (git-ignored) and write only
the keyed results into the project.

## Deliverables

Cut-outs, keyed to transparency, into `Assets/Game/UI/Art/Generated/`:

| File | Subject | Shape |
| --- | --- | --- |
| `Mascot-Idle.png` | mascot standing, relaxed, looking at the player | tall portrait |
| `Mascot-Cheer.png` | same mascot, celebrating a good score | tall portrait |
| `Mascot-Title.png` | same mascot, confident hero pose for the title screen | tall portrait |
| `Mascot-Bust.png` | head-and-shoulders of the same mascot, for a small avatar | square |
| `RankMedallion.png` | an empty circular medallion — ornate ring, glow, sparkles, **hollow centre**, no letter in it | square |
| `SparkleBurst.png` | a scatter of stars and sparkles for the result screen | square |
| `TitleEmblem.png` | an abstract emblem mark — headphone-and-soundwave motif, no lettering | square |

Full-bleed backgrounds (no keying needed, generate them opaque) into the same folder, 16:9:

| File | Subject |
| --- | --- |
| `Bg-Title.png` | neon city-at-night abstraction, deep indigo, soft bokeh, empty centre |
| `Bg-SongSelect.png` | calmer abstract gradient with soft geometric shapes, empty right two-thirds |
| `Bg-Result.png` | bright celebratory abstraction, confetti-ish, empty middle |

Backgrounds must stay quiet: text and panels sit on top of them, so keep contrast low and leave the
areas noted above uncluttered. No lettering anywhere in any asset — all text is rendered by the game.

Finally write `Assets/Game/UI/Art/Generated/manifest.json`: for each file, its pixel size, the chroma
key used (or `none`), and one line on what it is. The UI side reads this to place the assets.

## Reporting

Report which assets you produced, which needed a second pass and why, and anything you could not
make work.
