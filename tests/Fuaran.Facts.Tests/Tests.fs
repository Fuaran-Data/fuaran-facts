module Fuaran.Facts.Tests.Tests

open System
open Expecto
open Fuaran.Facts
open Fuaran.Facts.Tests.Certify

let private d = DecimalText.parse
let private at = Instant.parse

let private q2 = {
    From = at "2026-04-01T00:00:00Z"
    To = at "2026-07-01T00:00:00Z"
    Label = Some "Q2-2026"
}

let private widget = {
    Hierarchy = "product"
    Path = [ "acme"; "widget-x" ]
}

let private computedScalarInputs = [
    "sha256:1a682bcc89e6417f401d90a50051940f24f2d52cd06efd117ec522a9bbf0adaa"
    "sha256:24784b6201704fe16bf715429e781c2c6b0b8dfe138f68d3bd976eec0e72ad0c"
    "run:2026-07-02"
]

let private rollup =
    Computed("rollup", "2", "sha256:ffaf48da5032d34dffdd6cbd98f6190bfb534d309c233e434cfbf0c0b4314d51")

/// A fact with its id computed, the supersedes edge unset.
let private factOf
    (subject: SubjectRef)
    (metric: string)
    (period: TemporalExtent)
    (method: MethodRef)
    (value: FactValue)
    (inputs: string list)
    (asOf: string)
    : Fact =
    let draft = {
        FactId = ""
        Subject = subject
        Metric = MetricRef metric
        Value = value
        Period = period
        AsOf = at asOf
        Method = method
        Evidence = {
            ResultRef = None
            InputHashes = inputs
            TriggerRef = None
        }
        Confidence = None
        Supersedes = None
        Disclosure = Surfaceable
    }

    {
        draft with
            FactId = Fact.computeFor draft
    }

let private ok (r: Result<'T, Refusal>) : 'T =
    match r with
    | Ok v -> v
    | Error e -> failtestf "refused: %s" (Refusal.describe e)

let private refusedFor (expected: RefusalClass) (r: Result<'T, Refusal>) =
    match r with
    | Ok _ -> failtestf "expected a %s refusal, and it was accepted" (RefusalClass.code expected)
    | Error e -> Expect.equal e.Class expected (Refusal.describe e)

let identityTests =
    testList "identity (§4)" [
        test "the frame of §4.1 is length-prefixed" {
            Expect.equal (Frame.text [ "ab"; "c" ]) "2:ab1:c" "two fields"
            Expect.equal (Frame.text [ "a"; "bc" ]) "1:a2:bc" "the other cut"
            Expect.equal (Frame.text [ "" ]) "0:" "the empty string frames as 0:"
        }

        test "lengths are UTF-16 code units and the bytes are UTF-8 (§4.7)" {
            let bytes = FactWire.utf8 (Frame.text [ "é"; "😀" ])

            Expect.equal
                bytes
                [|
                    0x31uy
                    0x3auy
                    0xc3uy
                    0xa9uy
                    0x32uy
                    0x3auy
                    0xf0uy
                    0x9fuy
                    0x98uy
                    0x80uy
                |]
                "§4.7's bytes"

            Expect.stringStarts (Frame.text [ "store/zürich>café-😀" ]) "20:" "19 code points, 20 code units"
        }

        test "the worked computed example of §4.7, field by field" {
            let fields =
                Fact.identityFields widget (MetricRef "revenue") q2 rollup computedScalarInputs

            Expect.equal
                (Frame.text fields)
                "4:fact21:product/acme>widget-x7:revenue58:2026-04-01T00:00:00.0000000Z..2026-07-01T00:00:00.0000000Z89:computed:rollup:2:sha256:ffaf48da5032d34dffdd6cbd98f6190bfb534d309c233e434cfbf0c0b4314d5114:run:2026-07-0271:sha256:1a682bcc89e6417f401d90a50051940f24f2d52cd06efd117ec522a9bbf0adaa71:sha256:24784b6201704fe16bf715429e781c2c6b0b8dfe138f68d3bd976eec0e72ad0c"
                "the frame §4.7 prints"

            Expect.equal
                (Frame.digest fields)
                "10d2b0e4ad62672234d0e62f66b72499dbb5c240099318a7030884aa26caefeb"
                "the id §4.7 prints"
        }

        test "the worked asserted example of §4.7" {
            let value = Scalar(d "412000.00")
            Expect.equal (Fact.valueText value) "scalar:412000.00" "the value text"

            Expect.equal
                (Fact.valueHash value)
                "3cf77e4b262892289e7360569bd52f0552c753dffab76d7ce4e7797222bd3a56"
                "the value hash"

            let f =
                factOf widget "revenue" q2 (HumanAsserted "finance-lead") value [] "2026-07-02T06:00:00Z"

            Expect.equal f.FactId "6c651c140fb7b4ece1675691ff8b6f9f18af6739dd6728b540e9c719efef83b5" "the id"

            let rescaled =
                factOf
                    widget
                    "revenue"
                    q2
                    (HumanAsserted "finance-lead")
                    (Scalar(d "412000.0"))
                    []
                    "2026-07-02T06:00:00Z"

            Expect.notEqual rescaled.FactId f.FactId "the scale of an asserted value is in its id"
        }

        test "a computed fact's id is not its value's, nor its asOf's" {
            let a =
                factOf widget "revenue" q2 rollup (Scalar(d "1")) computedScalarInputs "2026-07-02T06:00:00Z"

            let b =
                factOf widget "revenue" q2 rollup (Scalar(d "2")) (List.rev computedScalarInputs) "2026-08-01T00:00:00Z"

            Expect.equal a.FactId b.FactId "value, asOf and input order are outside the frame"
        }

        test "the lineage key is the four renderings joined by '|' (§6.1)" {
            let f =
                factOf widget "revenue" q2 rollup (Scalar(d "1")) computedScalarInputs "2026-07-02T06:00:00Z"

            Expect.equal
                (Fact.lineageKey f)
                "product/acme>widget-x|revenue|2026-04-01T00:00:00.0000000Z..2026-07-01T00:00:00.0000000Z|computed:rollup:2:sha256:ffaf48da5032d34dffdd6cbd98f6190bfb534d309c233e434cfbf0c0b4314d51"
                "the key §6.1 prints"

            Expect.equal
                (Fact.lineageDigest f)
                (Fuaran.Core.Hash.sha256Hex (Fact.lineageKey f))
                "the digest is over the key's UTF-8 bytes"
        }

        test "'|' is refused where it would make the lineage key ambiguous (§3.5)" {
            let f = factOf widget "revenue" q2 rollup (Scalar(d "1")) [] "2026-07-02T06:00:00Z"
            let reminted (g: Fact) = { g with FactId = Fact.computeFor g }

            refusedFor
                RefusalClass.TokenInvalid
                (FactWire.writeFact (
                    reminted {
                        f with
                            Metric = MetricRef "revenue|net"
                    }
                ))

            refusedFor
                RefusalClass.TokenInvalid
                (FactWire.writeFact (
                    reminted {
                        f with
                            Subject = {
                                widget with
                                    Hierarchy = "product|line"
                            }
                    }
                ))

            refusedFor
                RefusalClass.TokenInvalid
                (FactWire.writeFact (
                    reminted {
                        f with
                            Subject = {
                                widget with
                                    Path = [ "acme"; "widget|x" ]
                            }
                    }
                ))

            Expect.isOk
                (FactWire.writeFact (reminted { f with Method = HumanAsserted "a|b" }))
                "a method's members may carry '|': the method is the key's last field"
        }

        test "the lineage key leaves inputs out and the method in" {
            let a =
                factOf widget "revenue" q2 rollup (Scalar(d "1")) [ "x" ] "2026-07-02T06:00:00Z"

            let b =
                factOf widget "revenue" q2 rollup (Scalar(d "1")) [ "y" ] "2026-07-03T06:00:00Z"

            let c =
                factOf widget "revenue" q2 (Computed("rollup", "3", "p")) (Scalar(d "1")) [ "x" ] "2026-07-03T06:00:00Z"

            Expect.isTrue (Fact.sameLineage a b) "a new input supersedes within the lineage"
            Expect.isFalse (Fact.sameLineage a c) "another method version competes"
        }

        test "a forecast is derived from the timestamps" {
            let f = factOf widget "revenue" q2 rollup (Scalar(d "1")) [] "2026-05-01T00:00:00Z"
            Expect.isTrue (Fact.isForecast f) "valid time runs past the moment it was known"

            Expect.isFalse
                (Fact.isForecast {
                    f with
                        AsOf = at "2026-07-01T00:00:00Z"
                })
                "known at its end"
        }

        test "a series address is a content address or a host key, told apart by its text (§5.3)" {
            let content = "sha256:" + String.replicate 32 "5f"
            Expect.equal (SeriesAddress.tryParse content) (Some(ContentAddress content)) "a content address"

            Expect.equal (SeriesAddress.tryParse "objects/weekly@v14") (Some(HostKey "objects/weekly@v14")) "a host key"

            Expect.equal (SeriesAddress.tryParse ("sha1:" + String.replicate 20 "ab")) None "another algorithm"
            Expect.equal (SeriesAddress.tryParse "sha256:abc") None "a truncated content address"
            Expect.equal (SeriesAddress.tryParse "") None "empty"

            let f =
                factOf
                    widget
                    "revenue_weekly"
                    q2
                    (HumanAsserted "finance-lead")
                    (Series(HostKey "objects/weekly@v14", None))
                    []
                    "2026-07-02T06:00:00Z"

            Expect.equal (Fact.valueText f.Value) "series:objects/weekly@v14" "the key enters the value text as carried"
            Expect.equal (ok (FactWire.readFactText (ok (FactWire.writeFact f)))) f "a host-key series round-trips"
        }

        test "the input-hash form" {
            Expect.isTrue (InputHash.isContentHash ("sha256:" + String.replicate 32 "ab")) "a sha256"
            Expect.isFalse (InputHash.isContentHash "run:2026-07-02") "a run token"
            Expect.isFalse (InputHash.isContentHash ("SHA256:" + String.replicate 32 "ab")) "upper-case algorithm"
            Expect.isFalse (InputHash.isContentHash ("sha256:" + String.replicate 15 "ab")) "fewer than 32 digits"
            Expect.isFalse (InputHash.isContentHash ("sha256:" + String.replicate 32 "ab" + "a")) "odd digits"

            Expect.equal
                (InputHash.named {
                    ResultRef = None
                    InputHashes = computedScalarInputs
                    TriggerRef = None
                })
                (List.take 2 computedScalarInputs)
                "the run token is not data"
        }
    ]

let sameInputsTests =
    testList "same inputs, same ids" [
        for s in SameInputs.samples do
            test s.Name { Expect.equal (SameInputs.idOf s) s.Expected "the id an existing deployment mints" }
    ]

let scalarTests =
    testList "decimal text and instants (§3.3, §3.4)" [
        test "decimal text: the grammar and the bounds" {
            for good in
                [
                    "0"
                    "-1"
                    "412000.00"
                    "0.0000000000000000000000000001"
                    "79228162514264337593543950335"
                    "-79228162514264337593543950335"
                ] do
                Expect.equal (d good).Text good good

            for bad in
                [
                    "1e3"
                    "+1"
                    "01"
                    "-0"
                    "-0.00"
                    "1."
                    ".5"
                    ""
                    " 1"
                    "79228162514264337593543950336"
                    "0.00000000000000000000000000001"
                ] do
                refusedFor RefusalClass.DecimalInvalid (DecimalText.tryParse bad)
        }

        test "decimal text: the scale is significant, comparison is numeric" {
            Expect.notEqual (d "1.5") (d "1.50") "two values"
            Expect.equal (DecimalText.compare (d "1.5") (d "1.50")) 0 "numerically equal"
            Expect.equal (DecimalText.compare (d "-2") (d "1.99")) -1 "ordered"
        }

        test "decimal text: a .NET decimal converts with its scale" {
            Expect.equal (DecimalText.ofDecimal 412000.00m).Text "412000.00" "scale kept"
            Expect.equal (DecimalText.toDecimal (d "412000.00")).Scale 2uy "scale kept back"
            Expect.equal (DecimalText.ofDecimal Decimal.MinValue).Text "-79228162514264337593543950335" "the bound"
        }

        test "instants: the wire text and the identity rendering" {
            Expect.equal (Instant.toWire (at "2026-04-01T00:00:00Z")) "2026-04-01T00:00:00Z" "whole seconds"
            Expect.equal (Instant.toIdentity (at "2026-04-01T00:00:00Z")) "2026-04-01T00:00:00.0000000Z" "seven digits"
            Expect.equal (Instant.toWire (at "2026-07-01T09:30:00.5Z")) "2026-07-01T09:30:00.5Z" "no trailing zero"
            Expect.equal (Instant.toIdentity (at "2026-07-01T09:30:00.5Z")) "2026-07-01T09:30:00.5000000Z" "padded"
            Expect.equal (Instant.toWire (at "9999-12-31T23:59:59.9999999Z")) "9999-12-31T23:59:59.9999999Z" "the end"
            Expect.equal (Instant.toWire (at "2024-02-29T12:00:00Z")) "2024-02-29T12:00:00Z" "a leap day"
        }

        test "instants: refusals" {
            for bad in
                [
                    "2026-04-01T00:00:00+00:00"
                    "2026-04-01T00:00:00"
                    "2026-02-30T00:00:00Z"
                    "2026-04-01T00:00:00.500Z"
                    "2026-04-01T00:00:00.12345678Z"
                    "2026-04-01T24:00:00Z"
                    "0000-01-01T00:00:00Z"
                    "2025-02-29T00:00:00Z"
                ] do
                refusedFor RefusalClass.InstantInvalid (Instant.tryParse bad)
        }

        test "instants: ticks are a DateTime's ticks" {
            let dt = DateTime(2026, 7, 1, 9, 30, 0, DateTimeKind.Utc).AddTicks 1234567L
            let i = Instant.ofDateTime dt
            Expect.equal i.Ticks dt.Ticks "the same count"
            Expect.equal (Instant.toIdentity i) (dt.ToString "o") "the identity rendering is the round-trip format"
            Expect.equal (Instant.toDateTime i) dt "back"

            let unspecified = DateTime(2026, 7, 1, 9, 30, 0, DateTimeKind.Unspecified)

            Expect.equal
                (Instant.ofDateTime unspecified)
                (at "2026-07-01T09:30:00Z")
                "an Unspecified value is read as UTC"
        }
    ]

let lawTests =
    testList "the laws (§6)" [
        let v1 =
            factOf widget "revenue" q2 rollup (Scalar(d "1")) [ "a" ] "2026-07-02T00:00:00Z"

        let v2 =
            factOf widget "revenue" q2 rollup (Scalar(d "2")) [ "b" ] "2026-07-03T00:00:00Z"

        let rival =
            factOf widget "revenue" q2 (Computed("model", "1", "p")) (Scalar(d "3")) [] "2026-07-03T00:00:00Z"

        test "accepting derives supersedes from the lineage head" {
            let h1, a1 = ok (History.accept [] v1)
            let h2, a2 = ok (History.accept h1 v2)
            let h3, a3 = ok (History.accept h2 rival)
            Expect.isNone a1.Supersedes "the first of its lineage"
            Expect.equal a2.Supersedes (Some v1.FactId) "the head it replaced"
            Expect.isNone a3.Supersedes "a competing lineage starts afresh"
            Expect.equal (History.check h3) (Ok()) "the history obeys L1-L3"
            Expect.isTrue (History.isHead h3 a2) "v2 is its lineage's head"
            Expect.isFalse (History.isHead h3 a1) "v1 is superseded"
            Expect.isTrue (History.extends h2 h3) "acceptance extends"
        }

        test "accepting an id already present changes nothing (L2)" {
            let h1, _ = ok (History.accept [] v1)

            let h2, again =
                ok (
                    History.accept h1 {
                        v1 with
                            AsOf = at "2027-01-01T00:00:00Z"
                    }
                )

            Expect.equal h2 h1 "the history converges"
            Expect.equal again.AsOf v1.AsOf "the fact it already holds"
        }

        test "accepting refuses a stale id and a superseder that is not later" {
            refusedFor
                RefusalClass.FactIdMismatch
                (History.accept [] {
                    v1 with
                        FactId = String.replicate 64 "0"
                })

            let h1, _ = ok (History.accept [] v2)
            refusedFor RefusalClass.SupersedesNotLater (History.accept h1 v1)
        }

        test "as-of visibility (L4)" {
            let h1, _ = ok (History.accept [] v1)
            let h2, _ = ok (History.accept h1 v2)
            Expect.equal (History.visibleAt h2 (at "2026-07-01T00:00:00Z")) [] "before any"
            Expect.equal (History.visibleAt h2 (at "2026-07-02T12:00:00Z")) [ v1.FactId ] "before the revision"
            Expect.equal (History.visibleAt h2 (at "2026-07-03T00:00:00Z")) [ v2.FactId ] "after the revision"
        }

        test "freshness (§6.6)" {
            let f = v1

            Expect.equal
                (Freshness.derive (FreshFor 86400) f true (at "2026-07-03T00:00:00Z"))
                Fresh
                "at the window's end"

            Expect.equal
                (Freshness.derive (FreshFor 86400) f true (at "2026-07-03T00:00:00.0000001Z"))
                (Stale(at "2026-07-03T00:00:00Z"))
                "a tick after"

            Expect.equal
                (Freshness.derive UntilSuperseded f false (at "2026-07-03T00:00:00Z"))
                (Stale f.AsOf)
                "superseded"

            Expect.equal (Freshness.derive UntilUpstreamChange f true (at "2030-01-01T00:00:00Z")) Fresh "current"
        }

        test "a written history that breaks a law is refused at the writer" {
            let forged = [ v1; { v2 with Supersedes = None } ]
            refusedFor RefusalClass.SupersedesMissing (FactWire.write (FactHistoryDocument forged))
        }
    ]

let private revenue: MetricDefinition = {
    Id = "revenue"
    Name = "Revenue"
    Unit = ok (MetricUnit.tryParse "[GBP]")
    Dimensionality = "currency"
    Direction = HigherIsBetter
    DisplayFormat = {
        Style = Currency
        FractionDigits = Some 0
        Grouping = true
    }
    Staleness = UntilSuperseded
    ProducingOperation = Some "rollup"
    CanonicalMethod = Some "computed:rollup"
    RollUp = Some(Additive(d "0.50"))
    Context = Some "Net invoiced revenue after refunds, recognised in the period the order shipped."
    RecomputePolicy = None
}

let definitionTests =
    testList "definitions (§7)" [
        test "a metric definition writes the corpus's bytes and digest" {
            let text = ok (FactWire.write (MetricDefinitionDocument revenue))

            Expect.equal
                text
                """{"kind":"metricDefinition","wireVersion":1,"id":"revenue","name":"Revenue","unit":"[GBP]","dimensionality":"currency","direction":"higherIsBetter","displayFormat":{"style":"currency","fractionDigits":0,"grouping":true},"staleness":{"kind":"untilSuperseded"},"producingOperation":"rollup","canonicalMethod":"computed:rollup","rollUp":{"kind":"additive","tolerance":"0.50"},"context":"Net invoiced revenue after refunds, recognised in the period the order shipped."}"""
                "definition/metric-revenue"

            Expect.equal
                (FactWire.metricDigest revenue)
                (Ok(FactWire.definitionDigest (FactWire.utf8 text)))
                "the digest is over the bytes"
        }

        test "units: canonical text only, and the currency is the unit's" {
            refusedFor RefusalClass.UnitInvalid (MetricUnit.tryParse "GBP")
            refusedFor RefusalClass.UnitInvalid (MetricUnit.tryParse "kg.h-1")
            Expect.equal (MetricUnit.text (ok (MetricUnit.tryParse "kg/h"))) "kg/h" "canonical"
            Expect.equal (MetricUnit.currency (ok (MetricUnit.tryParse "[GBP]"))) (Some "GBP") "a currency"
            Expect.equal (MetricUnit.currency (ok (MetricUnit.tryParse "k[GBP]"))) (Some "GBP") "prefixed"
            Expect.equal (MetricUnit.currency (ok (MetricUnit.tryParse "[GBP]/h"))) None "a rate is not a currency"
            Expect.equal (MetricUnit.currency (ok (MetricUnit.tryParse "[GBP]2"))) None "a square is not a currency"
            Expect.equal (MetricUnit.currency (ok (MetricUnit.tryParse "1"))) None "a count is not"
        }

        test "a currency format over a non-currency unit is refused" {
            let bad = {
                revenue with
                    Unit = ok (MetricUnit.tryParse "1")
            }

            refusedFor RefusalClass.DisplayFormatInvalid (Definition.checkMetric bad)
            refusedFor RefusalClass.DisplayFormatInvalid (FactWire.write (MetricDefinitionDocument bad))
        }

        test "the canonical-method selector matches at a ':' boundary" {
            Expect.isTrue (CanonicalMethod.matches "computed:rollup" "computed:rollup:2:p7") "a prefix"
            Expect.isTrue (CanonicalMethod.matches "asserted:cfo" "asserted:cfo") "exact"
            Expect.isFalse (CanonicalMethod.matches "computed:rollup" "computed:rollup-alt:1:p0") "not mid-token"
        }

        test "registries are ordered and unique" {
            refusedFor
                RefusalClass.RegistryUnordered
                (FactWire.write (MetricRegistryDocument [ revenue; { revenue with Name = "Again" } ]))
        }
    ]

/// The unit strings stored metric definitions carry, and what each converts to (DECISIONS.md D5).
let legacyTests =
    testList "conversion from free-text units and format strings" [
        test "every census unit converts as DECISIONS.md D5 states" {
            for legacy, expected in
                [
                    "GBP", "[GBP]"
                    "USD", "[USD]"
                    "count", "1"
                    "items", "1"
                    "units", "1"
                    "ratio", "1"
                    "bytes", "1"
                    "1", "1"
                    "%", "%"
                    "ms", "ms"
                ] do
                Expect.equal (Legacy.unitOf legacy |> Result.map MetricUnit.text) (Ok expected) legacy
        }

        test "a unit the table does not hold is refused, not guessed" {
            for legacy in [ ""; "gbp"; "B"; "tokens"; "north" ] do
                refusedFor RefusalClass.UnitInvalid (Legacy.unitOf legacy)
        }

        test "standard numeric format strings and the four plain custom patterns convert; others are refused" {
            let fmt style digits grouping = {
                Style = style
                FractionDigits = digits
                Grouping = grouping
            }

            for legacy, expected in
                [
                    "", DisplayFormat.verbatim
                    "N0", fmt Number (Some 0) true
                    "N2", fmt Number (Some 2) true
                    "n", fmt Number (Some 2) true
                    "F3", fmt Number (Some 3) false
                    "P1", fmt Percent (Some 1) true
                    "C0", fmt Currency (Some 0) true
                    "C2", fmt Currency (Some 2) true
                    "0", fmt Number (Some 0) false
                    "0.00", fmt Number (Some 2) false
                    "#,##0", fmt Number (Some 0) true
                    "#,##0.00", fmt Number (Some 2) true
                    "0.0%", fmt Percent (Some 1) false
                    "#,##0.0%", fmt Percent (Some 1) true
                ] do
                Expect.equal (Legacy.displayFormatOf legacy) (Ok expected) legacy

            for legacy, replacement in
                [
                    "£#,##0.00", "'C2'"
                    "£#,##0", "'C0'"
                    "#,###", "'N0'"
                    "0.##", "'F2'"
                    "#0.0%", "'P1'"
                    "G", "'C0'"
                    "E2", "'C0'"
                    "N29", "28"
                    "N100", "'N0'"
                ] do
                match Legacy.displayFormatOf legacy with
                | Ok _ -> failtestf "%s was accepted" legacy
                | Error r ->
                    Expect.equal r.Class RefusalClass.DisplayFormatInvalid legacy
                    Expect.stringContains r.Message replacement (legacy + " names its replacement")
        }

        test "a staleness window converts in whole seconds" {
            Expect.equal (Legacy.windowSecondsOf (TimeSpan.FromDays 1.0)) (Ok 86400) "a day"
            refusedFor RefusalClass.ShapeInvalid (Legacy.windowSecondsOf (TimeSpan.FromMilliseconds 1500.0))
            refusedFor RefusalClass.ShapeInvalid (Legacy.windowSecondsOf TimeSpan.Zero)
        }
    ]

/// The rendering standard is pinned by the corpus's `rendering` family on both legs; these hold the
/// package's own surface to it.
let renderingTests =
    testList "rendering (§10)" [
        test "rule 1 is the standard, and another rule is refused" {
            Expect.equal FactRendering.Rule 1 "rule 1"

            Expect.equal
                (FactRendering.renderUnder 1 (Some revenue) (Scalar(d "-1234.5")))
                (Ok "-£1,235")
                "a negative currency figure leads with '-'"

            refusedFor RefusalClass.VersionUnsupported (FactRendering.renderUnder 2 (Some revenue) (Scalar(d "1")))
        }

        test "a rendered value whose text is not the rule's is refused" {
            let doc text =
                RenderedValueDocument {
                    Rule = 1
                    Metric = Some revenue
                    Value = Scalar(d "-1234.5")
                    Text = text
                }

            Expect.isOk (FactWire.write (doc "-£1,235")) "the rule's text"
            refusedFor RefusalClass.RenderingMismatch (FactWire.write (doc "(£1,235)"))
        }

        test "a prefixed currency unit shows its prefix after the number" {
            let kgbp = {
                revenue with
                    Unit = ok (MetricUnit.tryParse "k[GBP]")
                    DisplayFormat = {
                        Style = Currency
                        FractionDigits = Some 2
                        Grouping = true
                    }
            }

            Expect.equal (FactRendering.renderFor (Some kgbp) (Scalar(d "12.5"))) "£12.50k" "k[GBP]"
        }
    ]

let recomputeTests =
    testList "the recompute policy (the host's, off the wire)" [
        test "a definition's bytes and digest do not carry it" {
            for policy in [ Some Eager; Some OnQuery; Some Manual ] do
                let withPolicy = {
                    revenue with
                        RecomputePolicy = policy
                }

                Expect.equal
                    (FactWire.write (MetricDefinitionDocument withPolicy))
                    (FactWire.write (MetricDefinitionDocument revenue))
                    "the same bytes"

                Expect.equal (FactWire.metricDigest withPolicy) (FactWire.metricDigest revenue) "the same digest"
        }

        test "a definition read from the wire declares none" {
            let text =
                ok (
                    FactWire.write (
                        MetricDefinitionDocument {
                            revenue with
                                RecomputePolicy = Some Eager
                        }
                    )
                )

            match FactWire.readText text with
            | Ok {
                     Document = MetricDefinitionDocument read
                 } -> Expect.equal read.RecomputePolicy None "undeclared"
            | other -> failtestf "read as %A" other
        }
    ]

let legacyFactTests =
    testList "the legacy saved shape (read, never written)" [
        for s in LegacyShape.samples do
            test s.Name {
                let f = ok (LegacyFact.read s.Saved)
                Expect.equal (Fact.lineageKey f) s.LineageKey "the lineage key that implementation indexed"
                Expect.equal f.FactId (Fact.computeFor f) "the id recomputes"
            }

        test "a quantity's text and an instant's ticks are kept" {
            let saved = (LegacyShape.samples |> List.find (fun s -> s.Name = "scalar")).Saved
            let f = ok (LegacyFact.read saved)
            Expect.equal f.Value (Scalar(d "1234.50")) "the scale is the number's text"
            Expect.equal (Instant.toWire f.AsOf) "2026-04-02T06:30:00.5Z" "seven digits, trailing zeros dropped"
            Expect.equal f.Confidence (Some { Value = d "0.9"; Basis = None }) "the confidence"
        }

        test "a date with no zone is UTC, and a series key is a host key" {
            let noZone =
                (LegacyShape.samples |> List.find (fun s -> s.Name = "scalar-no-zone")).Saved

            Expect.equal (Instant.toWire (ok (LegacyFact.read noZone)).AsOf) "2026-04-02T06:30:00.5Z" "UTC"
            let series = (LegacyShape.samples |> List.find (fun s -> s.Name = "series")).Saved

            Expect.equal
                (ok (LegacyFact.read series)).Value
                (Series(HostKey "objects/throughput@v12", None))
                "the store's own version key"
        }

        test "what the wire would refuse, the legacy reader refuses" {
            let saved = (LegacyShape.samples |> List.find (fun s -> s.Name = "scalar")).Saved
            refusedFor RefusalClass.FactIdMismatch (LegacyFact.read (saved.Replace("\"op.sum\"", "\"op.max\"")))

            refusedFor
                RefusalClass.InstantInvalid
                (LegacyFact.read (saved.Replace("06:30:00.5000000Z", "06:30:00+01:00")))

            refusedFor RefusalClass.DecimalInvalid (LegacyFact.read (saved.Replace("1234.50", "1.2345E3")))
            refusedFor RefusalClass.KindUnknown (LegacyFact.read (saved.Replace("\"Scalar\"", "\"Matrix\"")))
            refusedFor RefusalClass.TokenInvalid (LegacyFact.read (saved.Replace("\"throughput\"", "\"through|put\"")))

            let series = (LegacyShape.samples |> List.find (fun s -> s.Name = "series")).Saved

            refusedFor
                RefusalClass.ShapeInvalid
                (LegacyFact.read (series.Replace("objects/throughput@v12", "sha1:" + String.replicate 20 "ab")))
        }

        test "every sample, as the JavaScript leg checks it" {
            Expect.isEmpty (LegacyShape.failures ()) "every saved fact reads back"
        }
    ]

let codecTests =
    testList "the codec (§3)" [
        test "a fact written is the fact read" {
            let f = {
                factOf
                    widget
                    "revenue"
                    q2
                    rollup
                    (Distribution(Map.ofList [ "p90", d "3"; "P10", d "1.0" ]))
                    computedScalarInputs
                    "2026-07-02T06:00:00Z" with
                    Confidence =
                        Some {
                            Value = d "0.9"
                            Basis = Some "fit r2"
                        }
                    Disclosure = Restricted "licensed"
            }

            let text = ok (FactWire.writeFact f)
            Expect.equal (ok (FactWire.readFactText text)) f "round trip"
            Expect.stringContains text "\"buckets\":{\"P10\":\"1.0\",\"p90\":\"3\"}" "map keys in ordinal order"
        }

        test "a fact with a stale id is refused at the writer" {
            let f = factOf widget "revenue" q2 rollup (Scalar(d "1")) [] "2026-07-02T06:00:00Z"
            refusedFor RefusalClass.FactIdMismatch (FactWire.writeFact { f with Metric = MetricRef "visits" })
        }

        test "control characters escape with lower-case hex" {
            let f =
                factOf
                    widget
                    "revenue"
                    q2
                    (HumanAsserted "a\u001fb\nc\"d\\")
                    (Categorical "x\u0001")
                    []
                    "2026-07-02T06:00:00Z"

            let text = ok (FactWire.writeFact f)
            Expect.stringContains text "\"principal\":\"a\\u001fb\\nc\\\"d\\\\\"" "escapes"
        }

        test "bytes that are not UTF-8, and a byte-order mark, are not canonical" {
            refusedFor RefusalClass.NotCanonical (FactWire.read [| 0xC0uy; 0x80uy |])

            refusedFor
                RefusalClass.NotCanonical
                (FactWire.read (Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (FactWire.utf8 "{}")))

            refusedFor RefusalClass.ShapeInvalid (FactWire.readText "not json")
        }

        test "a lone surrogate is refused" {
            refusedFor
                RefusalClass.ShapeInvalid
                (FactWire.readText "{\"kind\":\"fact\",\"wireVersion\":1,\"id\":\"\\ud800\"}")
        }
    ]

let private corpusOrFail () =
    match Corpus.loadDefault () with
    | Ok loaded -> loaded
    | Error why -> failtest why

let corpusTests =
    testList "certification against the corpus (§9)" [
        test "every vector and every relation of the corpus" {
            let manifest, readFile = corpusOrFail ()
            let outcome = Certify.run readFile manifest

            for id, why in outcome.Failures do
                printfn "  FAIL %s: %s" id why

            Expect.isEmpty outcome.Failures "every vector certifies"
            Expect.equal outcome.Executed outcome.Listed "executed every listed vector"
            Expect.isGreaterThan outcome.Listed 0 "the manifest lists vectors"
            Expect.equal outcome.RelationsChecked manifest.Relations.Length "checked every relation"
            printfn "certified %d vectors and %d relations" outcome.Executed outcome.RelationsChecked
        }

        test "a mutated document makes the harness go red" {
            let manifest, readFile = corpusOrFail ()
            let byId = manifest.Vectors |> List.map (fun v -> v.Id, v) |> Map.ofList
            let v = byId |> Map.find "identity/computed-scalar"
            let original = readFile v.File
            let i = Array.findIndex (fun b -> b = byte '1') original
            let mutated = Array.copy original
            mutated[i] <- byte '2'

            let readMutated (file: string) =
                if file = v.File then mutated else readFile file

            Expect.isError (Certify.certifyVector readMutated byId v) "a changed byte is caught (by the file hash)"

            let unhashed = {
                v with
                    Sha256 = Fuaran.Core.Hash.sha256HexOfBytes mutated
            }

            Expect.isError
                (Certify.certifyVector readMutated byId unhashed)
                "and by the id check when the hash is updated"

            let wrongClass = byId |> Map.find "record/reject-whitespace"

            Expect.isError
                (Certify.certifyVector readFile byId {
                    wrongClass with
                        RejectClass = Some "shape-invalid"
                })
                "refusing for another class is not a pass"
        }

        test "an absent corpus fails rather than skips" {
            let missing =
                IO.Path.Combine(IO.Path.GetTempPath(), "no-such-corpus-" + Guid.NewGuid().ToString "N")

            Expect.isError (Corpus.load missing) "loading an absent corpus is an error, which fails the suite"
        }
    ]

let all =
    testList "Fuaran.Facts" [
        identityTests
        sameInputsTests
        scalarTests
        lawTests
        definitionTests
        legacyTests
        renderingTests
        recomputeTests
        legacyFactTests
        codecTests
        corpusTests
    ]
