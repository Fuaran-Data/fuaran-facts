# Fuaran.Facts — API stability

**Status:** pre-1.0. The version is single-sourced from `<Version>` in `Directory.Build.props`; this document
records what each version changed, by class.

## Versioning policy

Every package this repository produces ships at the one `<Version>`. A change to the public contract ships on a
version AHEAD of every version that has been tagged. The standing `<Version>` is a **draft slot** while it is
untagged: an additive change rides it (its entry grows here, the number does not move), and a change of a higher
class than the draft already carries advances it, because the number is what tells a consumer what adopting it
costs. Once a version is tagged, the next change to the public surface advances it in the same commit.

A release is a `v*` tag, which runs `.github/workflows/publish-packages.yml` (`DECISIONS.md` D11).

The wire format is versioned separately, by the Fact wire specification's `wireVersion` (1). A change to the wire
is the specification's to make first; this package follows it, and a package version that reads or writes a new
wire version is a breaking change here.

## 0.2.1 — DRAFT (Phase 37, 2026-11-16)

**An additive release.** A host's facts at rest that carry a token the specification refuses read back as stored,
and the package's Fable source compiles in a consumer that also compiles `Fuaran.Core.Column` (`DECISIONS.md` D14).
Certified against the Fact wire specification version 1, revision 1.2 (corpus of 150 vectors, on .NET and under
Fable).

**Additive — the reader's widened acceptance:**

- `LegacyFact.read` accepts, on data at rest only, the tokens FACT_WIRE.md §3.5 refuses: a path member containing
  `>` or `|`, or empty; a hierarchy containing `/` or `|`; a metric id containing `|`; a computed method's operation
  or version containing `:`; a distribution key containing `=` or `;`. Each such fact reads with its stored id
  (still verified) and its lineage key unchanged. Where a tolerated token makes two stored facts render alike, both
  read as stored (§3.5 names which tokens can, and what a host does). A legacy fact 0.2.0 refused for one of these
  tokens now reads; everything else it refused it still refuses.
- **Unchanged:** `FactWire` refuses every one of these tokens on read and on write, so no new fact carries one.

**None — the Fable fix:**

- `Definitions.fs`, `Text.fs` and `Json.fs` reach `Fuaran.Core.Unit` and `Fuaran.Core.Hash` through file-private
  module abbreviations, not by a qualified path in an expression, which a consumer compiling `Fuaran.Core.Column`'s
  source beside this package resolved to the union case `FieldChange.Unit` (18 errors). No public member changes.
  The JavaScript leg now compiles and runs the package beside `Fuaran.Core.Column` 0.42.0, the version consumers
  take; it does not also compile it beside the repository's own pin, 0.40.3 (`DECISIONS.md` D14).

## 0.2.0 — released 2026-11-16 as `v0.2.0`

**A breaking release.** A host whose stores already hold facts adopts the kernel without rewriting them, and the
package's rendering is the specification's rendering standard (`DECISIONS.md` D13). Certified against the Fact wire
specification version 1, revision 1.1 (corpus of 148 vectors and 13 relations, on .NET and under Fable).

**Release record.** Released from the Phase 35 tree (`62f7960`), whose gate (`pwsh ./verify.ps1`, full lane) ran
green: 71 tests passed and 0 failed, and 148 of 148 specification vectors were certified on .NET and under Fable.

**Breaking — what a consumer of 0.1.0 must change:**

- `FactValue.Series` carries a `SeriesAddress` (`ContentAddress` or `HostKey`) where it carried a string. Wrap an
  existing content address in `ContentAddress`; read one with `SeriesAddress.tryParse`.
- `MetricDefinition` gains `RecomputePolicy: RecomputePolicy option`, so every record expression that builds one
  names it (`None` keeps 0.1.0's meaning).
- `Fact.lineageKey` returns the specification's `|`-joined key (§6.1), not the length-prefixed frame. A caller that
  stored 0.1.0's keys holds different bytes.
- `FactDocument` gains `RenderedValueDocument`, and `RefusalClass` gains `RenderingMismatch`: an exhaustive match on
  either names the new case.
- `RenderGlyphs` and `FactRendering.renderWith` are removed, and `FactRendering.formatDecimal` takes no glyphs: the
  rendering's glyphs are fixed by the standard (§10). A host showing a localised form does so on top of the rendering,
  and never compares against it.
- **Behaviour.** The reader refuses `|` in a hierarchy, a path member and a metric id (`token-invalid`). A figure in a
  prefixed currency unit renders with the prefix after it (`£12.50k` for `12.5` in `k[GBP]`, where 0.1.0 wrote
  `£12.50`).

**Additive:**

- `SeriesAddress` and `SeriesAddress.tryParse`; a series named by an opaque host key reads and writes (§5.3).
- `Fact.lineageDigest` and `Fact.LineageSeparator`.
- `RecomputePolicy` (`Eager`, `OnQuery`, `Manual`), the host's policy, carried beside the wire members.
- `FactRendering.Rule` (1) and `FactRendering.renderUnder`; `RenderedValue` and the `renderedValue` document kind;
  `MetricUnit.currencyParts`.
- `LegacyFact.read`: a fact in the legacy saved shape, read and held to every rule the wire applies; never written.
- `Legacy.unitOf` converts `bytes` to `1`; `Legacy.displayFormatOf` converts the four plain custom patterns (`0`,
  `0.00`, `#,##0`, `#,##0.00`, each optionally followed by `%`), and a refused pattern's refusal names the standard
  format to write instead.

## 0.1.0 — released 2026-11-05 as `v0.1.0`

**Release record.** Released from the Phase 8 tree (`35df489`), whose gate (`pwsh ./verify.ps1`, full lane) ran
green: 75 tests passed and 0 failed, and 104 of 104 specification vectors were certified on .NET and under Fable. The
release commit changes only this record and the publish workflow's nuget.org login.

The first version: the Fact kernel, certified against the Fact wire specification version 1 (corpus of 104
vectors and 12 relations, on .NET and under Fable).

- **Kernel types:** `Fact`, `FactValue`, `MethodRef`, `Disclosure`, `SubjectRef`, `MetricRef`,
  `TemporalExtent`, `Evidence`, `Confidence`; `DecimalText` and `Instant` for the specification's decimal text
  and instants.
- **Definitions as data:** `MetricDefinition` (its unit a `Fuaran.Core.UnitOfMeasure`), `SubjectDefinition`,
  `DisplayFormat`, `StalenessPolicy`, `RollUp`, `DirectionOfBetter`; `MetricUnit`, `CanonicalMethod`,
  `Definition` checks; `Legacy` conversions from free-text units and .NET format strings.
- **Identity:** `Frame`, `Fact.compute` / `computeFor`, the value hash, the effective inputs, the lineage key,
  forecastness; `InputHash`.
- **Laws:** `History.check` / `accept` / `extends` / `visibleAt` / `headOf` / `isHead`; `Freshness`.
- **Codec:** `FactWire.read` / `readText` / `write` / `encode`, the eight document kinds, the definition digest;
  `Refusal` and the closed `RefusalClass` vocabulary.
- **Rendering:** `FactRendering`, `RenderGlyphs`, `CurrencySymbols`.
