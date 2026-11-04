/// Rendering vectors: a value shown as its metric declares (FACT_WIRE.md §7.1). Run on both legs,
/// so that a host-dependent decimal rendering — a platform's rounding mode, its grouping, a float
/// creeping into a decimal — is caught where it would differ.
module Fuaran.Facts.Tests.RenderingVectors

open Fuaran.Facts

let private unitOf (text: string) =
    match MetricUnit.tryParse text with
    | Ok u -> u
    | Error r -> failwith (Refusal.describe r)

let private metric (unit: string) (style: DisplayStyle) (digits: int option) (grouping: bool) : MetricDefinition = {
    Id = "m"
    Name = "M"
    Unit = unitOf unit
    Dimensionality = "any"
    Direction = Neutral
    DisplayFormat = {
        Style = style
        FractionDigits = digits
        Grouping = grouping
    }
    Staleness = UntilSuperseded
    ProducingOperation = None
    CanonicalMethod = None
    RollUp = None
    Context = None
}

let private d = DecimalText.parse

let private gbp0 = metric "[GBP]" Currency (Some 0) true
let private chf0 = metric "[CHF]" Currency (Some 0) true
let private kgbp2 = metric "k[GBP]" Currency (Some 2) true
let private n2 = metric "1" Number (Some 2) true
let private f0 = metric "1" Number (Some 0) false
let private p1 = metric "1" Percent (Some 1) true
let private verbatim = metric "[GBP]" Verbatim None false

/// (name, metric, value, expected text under the invariant glyphs).
let vectors: (string * MetricDefinition option * FactValue * string) list = [
    "currency, grouped", Some gbp0, Scalar(d "21240.00"), "£21,240"
    "currency, negative", Some gbp0, Scalar(d "-1234.5"), "-£1,235"
    "currency, rounds to zero without a sign", Some gbp0, Scalar(d "-0.4"), "£0"
    "currency, a code the table does not hold", Some chf0, Scalar(d "1299"), "CHF 1,299"
    "currency, prefixed unit", Some kgbp2, Scalar(d "12.5"), "£12.50"
    "number, half away from zero", Some n2, Scalar(d "2.345"), "2.35"
    "number, half away from zero, negative", Some n2, Scalar(d "-2.345"), "-2.35"
    "number, below the half", Some n2, Scalar(d "2.344999"), "2.34"
    "number, grouped", Some n2, Scalar(d "1234567.891"), "1,234,567.89"
    "number, padded", Some n2, Scalar(d "7"), "7.00"
    "number, ungrouped, no digits", Some f0, Scalar(d "1234.5"), "1235"
    "number, the 96-bit bound",
    Some n2,
    Scalar(d "79228162514264337593543950335"),
    "79,228,162,514,264,337,593,543,950,335.00"
    "number, 28 places", Some n2, Scalar(d "0.0000000000000000000000000001"), "0.00"
    "percent", Some p1, Scalar(d "0.12345"), "12.3%"
    "percent, half up", Some p1, Scalar(d "0.12355"), "12.4%"
    "percent, whole", Some p1, Scalar(d "1"), "100.0%"
    "verbatim keeps the scale", Some verbatim, Scalar(d "412000.00"), "412000.00"
    "no metric is verbatim", None, Scalar(d "412000.0"), "412000.0"
    "interval", Some n2, Interval(d "1.005", d "2"), "1.01 to 2.00"
    "distribution, ordinal order",
    Some n2,
    Distribution(Map.ofList [ "b", d "1"; "B", d "2"; "a", d "3" ]),
    "B: 2.00; a: 3.00; b: 1.00"
    "categorical", Some n2, Categorical "gold", "gold"
    "absent", Some n2, Absent "no data loaded", "absent (no data loaded)"
    "series",
    Some n2,
    Series("sha256:" + String.replicate 32 "5f", Some "v7"),
    "series sha256:" + String.replicate 32 "5f"
]

/// The vectors whose rendering differs: `(name, rendered, expected)`.
let failures () : (string * string * string) list =
    vectors
    |> List.choose (fun (name, m, v, expected) ->
        let actual = FactRendering.renderFor m v

        if actual = expected then
            None
        else
            Some(name, actual, expected))
