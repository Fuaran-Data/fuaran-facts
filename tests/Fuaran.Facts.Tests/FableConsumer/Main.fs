/// The consumer-shaped leg's entry point. Compiling is most of the test: this file, the package's source
/// and `Fuaran.Core.Column`'s source are one Fable project. Running it then checks that the package's
/// unit functions, compiled there, still answer as they do alone. Exits non-zero on any failure.
module Fuaran.Facts.Tests.FableConsumer.Main

open Fable.Core
open Fuaran.Facts

[<Emit("process.exit($0)")>]
let private exit (code: int) : unit = jsNative

let private failures: (string * string) list = [
    match MetricUnit.tryParse "[GBP]" with
    | Ok u ->
        if MetricUnit.text u <> "[GBP]" then
            "MetricUnit.text", MetricUnit.text u

        if MetricUnit.currency u <> Some "GBP" then
            "MetricUnit.currency", sprintf "%A" (MetricUnit.currency u)

        // A value of Core's own union, built here, so the consumer genuinely uses Column too.
        match Fuaran.Core.FieldChange.Unit(None, Some u) with
        | Fuaran.Core.FieldChange.Unit(_, Some after) when MetricUnit.text after = "[GBP]" -> ()
        | other -> "FieldChange.Unit", sprintf "%A" other
    | Error r -> "MetricUnit.tryParse", Refusal.describe r

    match Legacy.unitOf "count" with
    | Ok u when MetricUnit.text u = "1" -> ()
    | other -> "Legacy.unitOf count", sprintf "%A" other

    match Legacy.unitOf "GBP" with
    | Ok u when MetricUnit.text u = "[GBP]" -> ()
    | other -> "Legacy.unitOf GBP", sprintf "%A" other

    match MetricUnit.tryParse "[GBP]" with
    | Ok u ->
        match
            DisplayFormat.checkAt "f" u {
                Style = Currency
                FractionDigits = Some 0
                Grouping = true
            }
        with
        | Ok() -> ()
        | Error r -> "DisplayFormat.checkAt", Refusal.describe r
    | Error _ -> ()
]

for name, why in failures do
    eprintfn "  FAIL %s: %s" name why

printfn "fable consumer leg: the package compiled beside Fuaran.Core.Column; %d failure(s)" failures.Length
exit (if failures.IsEmpty then 0 else 1)
