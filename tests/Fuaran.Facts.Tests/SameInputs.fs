/// Same inputs, same ids. These ids were minted by an existing deployment's implementation of the
/// identity frame — the code this library was lifted from, run over these inputs on 2026-11-04 —
/// and are pinned here so that the lifted function is held to the ids that deployment's stores
/// already hold, not only to the corpus. The first two are also pinned in that implementation's own
/// suite, where they were computed independently of it. Run on both legs.
module Fuaran.Facts.Tests.SameInputs

open Fuaran.Facts

type Sample = {
    Name: string
    Subject: SubjectRef
    Metric: string
    Period: TemporalExtent
    Method: MethodRef
    Value: FactValue
    Inputs: string list
    Expected: string
}

let private d = DecimalText.parse

let private q2 = {
    From = Instant.parse "2026-04-01T00:00:00Z"
    To = Instant.parse "2026-07-01T00:00:00Z"
    Label = Some "Q2-2026"
}

let private inputA = "sha256:" + String.replicate 32 "ab"
let private inputB = "sha256:" + String.replicate 32 "0c"

let private geo = {
    Hierarchy = "geography"
    Path = [ "uk" ]
}

let samples: Sample list = [
    {
        Name = "golden-one-input"
        Subject = geo
        Metric = "revenue"
        Period = q2
        Method = Computed("rollup", "1", "p0")
        Value = Scalar(d "100")
        Inputs = [ inputA ]
        Expected = "40727a358c081002f3a3bfd84dc402727bdd7fe423445939e3ddde4fe3b78417"
    }
    {
        Name = "golden-two-inputs"
        Subject = geo
        Metric = "revenue"
        Period = q2
        Method = Computed("rollup", "1", "p0")
        Value = Scalar(d "100")
        Inputs = [ inputB; inputA ]
        Expected = "18428aedfa0d799356786c65ee4271ca25ce6d66e3be5a2845f7d1341b55441a"
    }
    {
        Name = "asserted-root-scalar"
        Subject = { Hierarchy = "geography"; Path = [] }
        Metric = "revenue"
        Period = q2
        Method = HumanAsserted "cfo"
        Value = Scalar(d "100.50")
        Inputs = []
        Expected = "d87826d4297a023e582fd51faa557b9b763cbf644766b342868c87ac8e01f913"
    }
    {
        Name = "asserted-interval-negative"
        Subject = geo
        Metric = "margin"
        Period = q2
        Method = HumanAsserted "cfo"
        Value = Interval(d "-1.5", d "2.25")
        Inputs = [ inputA ]
        Expected = "d55dde0f60e755c0c6a4be13ad9814f381fe03a37dd2a4317dee428dd3e8f054"
    }
    {
        Name = "asserted-distribution-mixed-case"
        Subject = geo
        Metric = "basket"
        Period = q2
        Method = HumanAsserted "analyst"
        Value = Distribution(Map.ofList [ "p50", d "3.0"; "P10", d "1"; "a", d "0.25"; "Z", d "9" ])
        Inputs = []
        Expected = "4d43149d02be58c81b5a87ace64d24ed10bf59879efeea53a3f6ffda29246a19"
    }
    {
        Name = "asserted-categorical-non-ascii"
        Subject = geo
        Metric = "segment"
        Period = q2
        Method = HumanAsserted "analyst"
        Value = Categorical "élan 😀"
        Inputs = []
        Expected = "f72d5fad190c2dbaefc7fa8053e006c2f9dda892cf2418b232caf3a66f1c644b"
    }
    {
        Name = "asserted-absent"
        Subject = geo
        Metric = "revenue"
        Period = q2
        Method = HumanAsserted "loader"
        Value = Absent "no data loaded for this period"
        Inputs = []
        Expected = "03634f1fe3e50c2f29365f1909a1638c0ae9f517a9114567ac86365f07ddb945"
    }
    {
        Name = "computed-empty-param-duplicate-inputs"
        Subject = geo
        Metric = "visits"
        Period = q2
        Method = Computed("count", "3", "")
        Value = Scalar(d "7")
        Inputs = [ "x"; inputA; "x" ]
        Expected = "f5e1f2b965c87a8a2a209a87e3fb7f8d0075cfe1fbb9ba3f26e1a92dada6feda"
    }
    {
        Name = "imported-fractional-ticks"
        Subject = geo
        Metric = "revenue"
        Period = {
            From = Instant.parse "2026-07-01T09:30:00.1234567Z"
            To = Instant.parse "2026-07-01T09:30:01.5Z"
            Label = None
        }
        Method = Imported "peer-1/cert-7"
        Value = Scalar(d "1")
        Inputs = [ inputB ]
        Expected = "db2fdaba63c9a1f1a0693b2bce78482adb683c57b5aefdcb935622d352decb05"
    }
    {
        Name = "computed-non-ascii-subject"
        Subject = {
            Hierarchy = "store"
            Path = [ "zürich"; "café-😀" ]
        }
        Metric = "café"
        Period = q2
        Method = Computed("rollup", "2", inputB)
        Value = Scalar(d "1")
        Inputs = [ inputA ]
        Expected = "e0b628987f316f32a57e70a248f4aaf8d96e8223bb412ddfbd1ad6868eca2c8b"
    }
    {
        Name = "asserted-series"
        Subject = geo
        Metric = "trace"
        Period = q2
        Method = HumanAsserted "ops"
        Value = Series(ContentAddress("sha256:" + String.replicate 32 "5f"), None)
        Inputs = []
        Expected = "e1fb5b617f3244196a6f099c9689b1d0f84577255ea2c5630c9a82ada6174f8e"
    }
    {
        Name = "computed-instant-period-year-one"
        Subject = geo
        Metric = "revenue"
        Period = {
            From = Instant.parse "0001-01-01T00:00:00Z"
            To = Instant.parse "0001-01-01T00:00:00Z"
            Label = None
        }
        Method = Computed("rollup", "1", "p0")
        Value = Scalar(d "0")
        Inputs = [ inputA ]
        Expected = "bd8f8de9c03e9fbbb990c95aa93f42ed2c4a4690f81f5cc562aa460b24a02fb9"
    }
    {
        Name = "asserted-decimal-bounds"
        Subject = geo
        Metric = "ratio"
        Period = q2
        Method = HumanAsserted "cfo"
        Value = Interval(d "0.0000000000000000000000000001", d "79228162514264337593543950335")
        Inputs = []
        Expected = "1c0afe4f9880cea4cc44f7ac811939494c058f24eb89de2b79e5266acb3ec3ea"
    }
    {
        Name = "asserted-scalar-scale-zero-padded"
        Subject = geo
        Metric = "revenue"
        Period = q2
        Method = HumanAsserted "cfo"
        Value = Scalar(d "412000.000")
        Inputs = [ inputA; inputB ]
        Expected = "8dde3c230488607d03d8d776b792ab056075d72bd11581c3ce7bbb16a60f8847"
    }
]

/// The id this library computes for a sample.
let idOf (s: Sample) : string =
    let evidence = {
        ResultRef = None
        InputHashes = s.Inputs
        TriggerRef = None
    }

    Fact.compute s.Subject (MetricRef s.Metric) s.Period s.Method (Fact.effectiveInputHashes s.Method evidence s.Value)

/// The samples whose computed id differs from the pinned one: `(name, computed, expected)`.
let failures () : (string * string * string) list =
    samples
    |> List.choose (fun s ->
        let id = idOf s

        if id = s.Expected then
            None
        else
            Some(s.Name, id, s.Expected))
