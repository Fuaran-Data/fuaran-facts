/// The JavaScript leg's entry point: node's `fs` and `JSON.parse` in place of System.IO and
/// System.Text.Json, and nothing else differs from the .NET leg. Exits non-zero on any failure, and
/// when the corpus is absent.
module Fuaran.Facts.Tests.Fable.Main

open Fable.Core
open Fuaran.Facts.Tests
open Fuaran.Facts.Tests.Certify

[<Import("readFileSync", "fs")>]
let private readFileSync (path: string) : byte[] = jsNative

[<Import("existsSync", "fs")>]
let private existsSync (path: string) : bool = jsNative

[<Emit("process.argv.slice(2)")>]
let private argv: string array = jsNative

[<Emit("process.exit($0)")>]
let private exit (code: int) : unit = jsNative

[<Emit("JSON.parse($0)")>]
let private jsonParse (text: string) : obj = jsNative

[<Emit("($0[$1] ?? null)")>]
let private field (o: obj) (name: string) : obj = jsNative

[<Emit("(typeof $0 === 'string' ? $0 : null)")>]
let private asString (o: obj) : string = jsNative

[<Emit("Array.from($0)")>]
let private asArray (o: obj) : obj array = jsNative

let private optString (o: obj) (name: string) : string option =
    match asString (field o name) with
    | null -> None
    | s -> Some s

let private manifestOf (text: string) : Manifest =
    let root = jsonParse text

    {
        Specification = asString (field root "specification")
        WireVersion = unbox<int> (field root "wireVersion")
        Vectors =
            asArray (field root "vectors")
            |> Array.map (fun v -> {
                Id = asString (field v "id")
                Family = asString (field v "family")
                Kind = asString (field v "kind")
                File = asString (field v "file")
                Sha256 = asString (field v "sha256")
                Digest = optString v "digest"
                RejectClass = optString v "rejectClass"
                SameAs = optString v "sameAs"
                LineageKey = optString v "lineageKey"
            })
            |> List.ofArray
        Relations =
            asArray (field root "relations")
            |> Array.map (fun r -> {
                Ids = optString r "ids"
                History = optString r "history"
                Vectors = asArray (field r "vectors") |> Array.map asString |> List.ofArray
            })
            |> List.ofArray
    }

/// The consumer-shaped checks: this project compiles the package's source beside the published
/// `Fuaran.Core.Column` source, so compiling is most of the test. Running then checks that the package's
/// unit functions, compiled there, answer as they do alone, and uses a value of Column's own union.
let private consumerFailures () : (string * string) list = [
    match Fuaran.Facts.MetricUnit.tryParse "[GBP]" with
    | Ok u ->
        if Fuaran.Facts.MetricUnit.text u <> "[GBP]" then
            "MetricUnit.text", Fuaran.Facts.MetricUnit.text u

        if Fuaran.Facts.MetricUnit.currency u <> Some "GBP" then
            "MetricUnit.currency", sprintf "%A" (Fuaran.Facts.MetricUnit.currency u)

        match Fuaran.Core.FieldChange.Unit(None, Some u) with
        | Fuaran.Core.FieldChange.Unit(_, Some after) when Fuaran.Facts.MetricUnit.text after = "[GBP]" -> ()
        | other -> "FieldChange.Unit", sprintf "%A" other

        match
            Fuaran.Facts.DisplayFormat.checkAt "f" u {
                Style = Fuaran.Facts.Currency
                FractionDigits = Some 0
                Grouping = true
            }
        with
        | Ok() -> ()
        | Error r -> "DisplayFormat.checkAt", Fuaran.Facts.Refusal.describe r
    | Error r -> "MetricUnit.tryParse", Fuaran.Facts.Refusal.describe r

    match Fuaran.Facts.Legacy.unitOf "count" with
    | Ok u when Fuaran.Facts.MetricUnit.text u = "1" -> ()
    | other -> "Legacy.unitOf count", sprintf "%A" other

    match Fuaran.Facts.Legacy.unitOf "GBP" with
    | Ok u when Fuaran.Facts.MetricUnit.text u = "[GBP]" -> ()
    | other -> "Legacy.unitOf GBP", sprintf "%A" other
]

let private run (corpus: string) : int =
    let manifestPath = corpus + "/manifest.json"

    if not (existsSync manifestPath) then
        eprintfn "the conformance corpus is not at '%s': it is a build input, and its absence fails this leg" corpus
        1
    else
        let utf8 = System.Text.Encoding.UTF8.GetString(readFileSync manifestPath)
        let manifest = manifestOf utf8
        let outcome = Certify.run (fun file -> readFileSync (corpus + "/" + file)) manifest

        for id, why in outcome.Failures do
            eprintfn "  FAIL %s: %s" id why

        let identity = SameInputs.failures ()

        for name, actual, expected in identity do
            eprintfn "  FAIL same-inputs %s: %s, expected %s" name actual expected

        let legacy =
            LegacyShape.failures ()
            @ LegacyShape.storedTokenFailures ()
            @ LegacyShape.ambiguityFailures ()

        for name, why in legacy do
            eprintfn "  FAIL legacy shape %s: %s" name why

        let consumer = consumerFailures ()

        for name, why in consumer do
            eprintfn "  FAIL beside Fuaran.Core.Column %s: %s" name why

        printfn
            "fable leg: certified %d of %d vectors and %d relations; %d same-input ids; %d legacy-shape facts, %d with a stored token, %d ambiguous pairs; compiled beside Fuaran.Core.Column"
            outcome.Executed
            outcome.Listed
            outcome.RelationsChecked
            SameInputs.samples.Length
            LegacyShape.samples.Length
            LegacyShape.storedTokenSamples.Length
            LegacyShape.ambiguities.Length

        let failed =
            outcome.Failures.Length + identity.Length + legacy.Length + consumer.Length

        if failed > 0 || outcome.Listed = 0 || outcome.Executed <> outcome.Listed then
            eprintfn "fable leg: %d failure(s)" failed
            1
        else
            0

match argv with
| [| corpus |] -> exit (run corpus)
| _ ->
    eprintfn "usage: node Main.js <wire-fixtures directory>"
    exit 2
