# Fuaran.Facts

The .NET reference implementation of the [Fact wire specification](https://github.com/Fuaran-Data/fuaran-fact-specification):
the Fact kernel's types, its canonical codec, the identity frame, supersession, as-of visibility, forecastness
and freshness, and metric and subject definitions as data, with a value rendered by the specification's rendering
standard. A store that already holds facts in the kernel's legacy saved shape reads them with `LegacyFact.read`,
with their ids and lineage keys unchanged.
`FSharp.Core` and two `Fuaran.Core` packages only, and Fable-clean: the same ids, bytes and renderings on .NET and
in JavaScript.

A **Fact** is a verified assertion that a metric of a subject had a value over a period, as known at an instant,
made by a stated method from stated inputs. Its id is the digest of an identity frame over those members, so the
same assertion made twice is one fact, and a correction is a new fact that supersedes the old one in its lineage.

```fsharp
open Fuaran.Facts

let period = { From = Instant.parse "2026-04-01T00:00:00Z"; To = Instant.parse "2026-07-01T00:00:00Z"; Label = Some "Q2-2026" }

let draft = {
    FactId = ""
    Subject = { Hierarchy = "product"; Path = [ "acme"; "widget-x" ] }
    Metric = MetricRef "revenue"
    Value = Scalar(DecimalText.parse "412000.00")
    Period = period
    AsOf = Instant.parse "2026-07-02T06:00:00Z"
    Method = HumanAsserted "finance-lead"
    Evidence = { ResultRef = None; InputHashes = []; TriggerRef = None }
    Confidence = None
    Supersedes = None
    Disclosure = Surfaceable
}

let fact = { draft with FactId = Fact.computeFor draft }   // 6c651c14...
let history, accepted = History.accept [] fact |> Result.defaultWith (Refusal.describe >> failwith)
let text = FactWire.writeFact accepted                       // the canonical bytes, read back before returned
```

## Layout

```
src/Fuaran.Facts/          the package
tests/Fuaran.Facts.Tests/  the Expecto suite; Certify.fs, SameInputs.fs and LegacyShape.fs are shared
                           with the JavaScript leg under Fable/
run.ps1                    build, test, certify on both legs, sweep the publication boundary
DECISIONS.md               why it is the way it is
STABILITY.md               what each version changed
```

## Build and verify

```powershell
pwsh ./run.ps1
```

PowerShell 7, the .NET SDK `global.json` pins, and Node.js. The conformance corpus is a **build input**: clone the
specification beside this repository (`../Fuaran-Fact-Specification`), or point `FUARAN_FACT_SPEC` at a clone.
Without it the gate fails rather than skips, because a conformance check that goes green without its oracle is
worse than none. `-SkipFormat`, `-SkipBuild`, `-SkipTests` and `-SkipFable` shorten the loop while iterating.

## What is deliberately not here

Storage, queries over populations of facts, disclosure enforcement, signatures and presentation beyond a value's
rendered text, and writing the legacy saved shape (it is read, never written). A Fact is portable data; how a host keeps it, who may see it and how it is shown are the host's
business. Disclosure labels are carried, never enforced.

## Relation to Fuaran Core

A metric's unit is a [`Fuaran.Core.Unit`](https://github.com/Fuaran-Core/fuaran-core/blob/main/docs/units.md)
value, a series is named by a `Fuaran.Core` content address (or an opaque host key), and the SHA-256 is `Fuaran.Core`'s pure
implementation. The fact identity frame is the specification's, distinct from Core's record identity.

## Licence

Apache-2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE). Copyright 2026 Diametrical Ltd.
