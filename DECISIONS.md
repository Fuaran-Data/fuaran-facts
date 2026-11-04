# Decisions

The record of why this package is the way it is. Each entry is numbered once and never renumbered; a later
decision that changes an earlier one says so and cites it.

---

## D1 — The repository: public at the cut, Apache-2.0 (Phase 8, 2026-11-04)

The repository is public from its first commit (operator, 2026-11-03), under Apache-2.0 with a `NOTICE`, on
`main`. It carries a `README.md` and this record and no private working instructions. The gate's last stage
(`run.ps1`, "boundary") sweeps every tracked file and every commit message for the names a public repository of
this family must never carry, holding the list as SHA-256 digests so that the gate does not publish what it
protects; the list is the Fact wire specification's, which guards the same boundary.

## D2 — A lift, not a rewrite (Phase 8, 2026-11-04)

`Fuaran.Facts` is the Fact kernel lifted from the implementation whose stores already hold fact ids, not written
afresh: the code that minted every stored id is the code that keeps minting it. What came across is the kernel the
specification describes — the record and its value, method, extent, evidence, confidence and disclosure label;
the identity frame, the value hash and the effective inputs; the input-hash form; the lineage key, forecastness and
freshness; the canonical-method selector; the currency-symbol table; and metric and subject definitions as data.
What stayed with that implementation is everything about storing, guarding and showing facts: stores and their
queries, disclosure enforcement (a viewer, an ordering of labels, a taint index, egress mapping), population,
aggregate and plan queries, publication, pipeline tools, and the recompute policy a host runs when an input
changes (the specification's D15).

Three things changed on the way across, each because the specification now says so:

1. **A quantity is decimal text and an instant is ticks** (D6), not a platform decimal and a platform date.
2. **A series names a content address, with an optional locator** (`Series of address * locator`), where the
   value used to be a host's own version key. A `humanAsserted` series fact therefore hashes `series:` + the
   address (the specification's D1 ruling 3); computed and imported series facts are unaffected.
3. **A metric definition's unit is a `Fuaran.Core.Unit` value and its display format a neutral record** (D5),
   where both were free strings.

**Premise checked: the ids agree.** Before anything was written, the published build of the original
implementation was run over the specification's 21 identity vectors and reproduced every id; the test suite
pins 14 further same-input ids it minted (`tests/Fuaran.Facts.Tests/SameInputs.fs`), two of which its own suite
had pinned against an independent computation. Both legs of this repository's gate recompute all of them.

## D3 — Restore path: public, from nuget.org only (Phase 8, 2026-11-04)

Every dependency is published on nuget.org, so `nuget.config` clears every source and names nuget.org alone, and
`Directory.Build.props` declares `<FuaranRestorePath>public</FuaranRestorePath>`. An outside clone restores
exactly what the gate restores. The `Fuaran.Core` packages are pinned through ONE property,
`FuaranCoreVersion` (0.40.3), because they are produced by one repository at one version.

The library takes `FSharp.Core`, `Fuaran.Core.Text` (the pure SHA-256 and the UTF-8 encoder) and
`Fuaran.Core.Unit` (the unit algebra), and nothing else. Both are `FSharp.Core`-only and Fable-clean, which is
what lets this package be both.

*Alternative.* A local folder feed for the development loop, as some siblings carry: rejected, because a
checkout's depth below a shared folder differs between a primary clone and a worktree, so no relative path is
right for both, and because nothing this package needs is unpublished.

## D4 — Not a cohort member (operator, 2026-11-03)

`Fuaran.Facts` is not a member of the coordinated release cohort of the central build packages, on the
reasoning `Fuaran.Eval.Core` recorded for itself: its consumers release on their own rhythm, and the version that
matters to each is the one whose types it compiles against. A cohort obligation would move this package for
reasons none of its consumers can see in its tree. It pins `Fuaran.Core` like any consumer, by one property,
raised when a reason to raise it exists. The producer-side rule is unaffected: a change to the public surface
advances `<Version>` by the draft-slot rule (`STABILITY.md`).

## D5 — Units and display formats: converted by table, refused rather than guessed (Phase 8, 2026-11-04)

The specification makes a metric's unit `Fuaran.Core.Unit`'s canonical text and its display format a neutral
record (its D5 and D6). Stored definitions written before it carry a free-text unit and a .NET numeric format
string, so `Legacy.unitOf` and `Legacy.displayFormatOf` convert them. Each conversion is a table, and anything the
table does not hold, and that is not already the thing it converts to, is refused, never guessed.

**Units.** A census of every metric definition stored in the source implementation and its consumers found these
units, and they convert as follows:

| Stored unit | Converts to | Why |
|---|---|---|
| `GBP` (and any three capitals) | `[GBP]` | an ISO 4217 code is a currency unit of its own dimension in Core |
| `count`, `items`, `units` | `1` | a dimensionless count is `1` (specification §7.2) |
| `ratio` | `1` | a dimensionless ratio |
| `1` | `1` | already canonical |
| `%` | `%` | already canonical unit text |

Anything else is read as unit text by `Fuaran.Core.Unit` (so `ms` and `[GBP]` pass), and refused (`unit-invalid`)
when it does not parse: the empty string, a lower-case code (`gbp`), and words outside the table such as
`bytes`, `tokens` or `B` (which Core reads as the bel, a logarithmic unit it refuses). The census also met such
strings in records that are not metric definitions (telemetry signals and unrelated fields); they are not
converted, and are listed only so the refusal is a known outcome rather than a surprise.

**Display formats.** `""` is `verbatim`. `N<d>` is a grouped `number`, `F<d>` an ungrouped one, `P<d>` a grouped
`percent` and `C<d>` a grouped `currency`, each with `d` fraction digits (two when omitted, the invariant
culture's default). A custom pattern (`#,##0.00`, `£#,##0`) is refused: its meaning belongs to a formatting
library, and a currency glyph written into a pattern is a second statement of the currency the unit already makes.
A `C` format over a unit that is not one currency (stored definitions carry `C0` over `count`) converts, and is
then refused by the definition's own check (`display-format-invalid`), because the specification refuses it.

**Rendering.** A value renders as its metric's format declares (`FactRendering.renderFor`), rounding half away
from zero on exact integer arithmetic, so the rendering is the same on every host. Two deliberate departures from
the .NET formats the stored strings named: a negative currency figure is written `-£1,235` rather than
`(£1,235)`, and a percent as `12.3%` rather than `12.3 %`. The specification fixes the number and leaves the
glyphs to the reader, and `RenderGlyphs` is where a reader states its own.

## D6 — Decimal text and ticks, not platform types (Phase 8, 2026-11-04)

A quantity is `DecimalText`: the text, validated against §3.3, compared numerically by `DecimalText.compare` and
never normalised. An instant is `Instant`: ticks since 0001-01-01T00:00:00Z, the count a .NET `DateTime` holds.
Both are what the specification's bytes are, held exactly on every host: a JavaScript date has millisecond
resolution and a JavaScript number is not a decimal, so a platform type would have made the Fable build hash and
render differently from the .NET one. `DecimalText.ofDecimal` / `toDecimal` and `Instant.ofDateTime` /
`toDateTime` convert on .NET (the scale of a decimal, and the ticks of a date, are kept); an `Unspecified` date is
read as UTC, so an id is a function of the fact alone and never of the zone of the machine computing it.

## D7 — The frame is the specification's, hashed by Core's pure SHA-256 (Phase 8, 2026-11-04)

The identity frame is the length-prefixed `<utf16-length>:<field>` frame the specification states in full, not
`Fuaran.Core`'s content address (the specification's D1 ruling 2). The digest is `Fuaran.Core.Hash.sha256Hex`,
Core's FIPS 180-4 implementation in plain F#, rather than the platform's cryptography, because the platform's does
not compile under Fable and Core's is certified to give the same value on both pipelines. The fact id is bare
hex; a series address and a definition digest carry `sha256:`.

## D8 — Disclosure is labels only (Phase 8, 2026-11-04)

`Disclosure` is the three labels and nothing else. Any ordering of labels, any meet of two of them, and any
string form a host prints them in for its own audit trail is enforcement or presentation, which the
specification leaves to the host (§1, §5.8), so none of it is here.

## D9 — The writer reads back what it writes (Phase 8, 2026-11-04)

`FactWire.write` encodes a document and reads the text back under every rule a reader applies, returning the
refusal if there is one. A fact whose members moved without its id, a history that breaks a law, or a definition
outside its grammar is therefore refused where it is written, not where it is received. `FactWire.encode` is the
unchecked form. There is one reader, and the writer's check is that reader, so the two cannot disagree.

## D10 — The JavaScript leg is a gate stage that runs the corpus, not only a compile (Phase 8, 2026-11-04)

"It compiles under Fable" and "it gives the same answers under Fable" are different claims, and only the second
matters to a browser host. So the certification code (`Certify.fs`), the same-input ids and the rendering vectors
are written once, without a test framework, and run on both legs: by Expecto on .NET, and compiled by Fable and
run under node against the same corpus. The legs differ only in reading a file and parsing the manifest. Both
fail, never skip, when the corpus is absent, and the gate fails when node is. The leg installs no package, so
`run.ps1` resolves the node application directly and shells no `npm`, `npx` or `pnpm`.

Under Fable, `Fuaran.Core` is compiled from source and its internal names come into scope with it, so no file
here opens the `Fuaran.Core` namespace: one of its internal union cases would shadow this library's `Currency`.

## D11 — 0.1.0, a draft, published by tag through Trusted Publishing (Phase 8, 2026-11-04)

The first version is 0.1.0, a DRAFT slot until it is tagged (`STABILITY.md`). `.github/workflows/publish-packages.yml`
packs on a `v*` tag only, logs in to nuget.org through Trusted Publishing (OIDC, no stored key), refuses any
package id outside `Fuaran.Facts.*`, and pushes with `--skip-duplicate`; a branch push reaches no publish job. The
nuget.org policy (owner `Fuaran-Data`, repository `fuaran-facts`, workflow `publish-packages.yml`) is registered
by the operator before the first tag, and the tag is the operator's act. The `Fuaran.*` id prefix on nuget.org is
reserved to the operator's profile (checked 2026-11-04: `Fuaran.Core.*` and `Fuaran.Eval.*` are listed as
verified), and `Fuaran.Facts` was unclaimed.

## D12 — Supersession is derived at acceptance (Phase 8, 2026-11-04)

`History.accept` is the one place `Supersedes` is set: it verifies the incoming fact's id, makes it supersede its
lineage's current head (or nothing, for a lineage's first fact), and refuses a fact whose `AsOf` does not exceed
that head's, since a history with it would break L3. Accepting an id the history already holds changes nothing
(L2). Whatever `Supersedes` a caller set on a draft is ignored, because the specification derives it and never
takes it from a producer.
