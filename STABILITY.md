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

## 0.1.0 — DRAFT

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
