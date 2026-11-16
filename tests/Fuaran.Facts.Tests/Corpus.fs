/// The .NET leg's loader: where the corpus is, and its manifest read with System.Text.Json.
module Fuaran.Facts.Tests.Corpus

open System
open System.IO
open System.Text.Json
open Fuaran.Facts.Tests.Certify

/// The environment variable that points at a specification clone, overriding the sibling path.
[<Literal>]
let SpecVariable = "FUARAN_FACT_SPEC"

/// The specification clone: `FUARAN_FACT_SPEC`, else `../Fuaran-Fact-Specification` beside this
/// repository. The corpus is a BUILD INPUT: its absence fails the suite, never skips it.
let specRoot () : string =
    match Environment.GetEnvironmentVariable SpecVariable with
    | null
    | "" -> Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "Fuaran-Fact-Specification"))
    | v -> Path.GetFullPath v

let private optString (e: JsonElement) (name: string) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString())
    | _ -> None

/// Reads the manifest of the corpus at `corpusDir` (the `wire-fixtures` directory), or says why it
/// cannot.
let load (corpusDir: string) : Result<Manifest * (string -> byte[]), string> =
    let manifestPath = Path.Combine(corpusDir, "manifest.json")

    if not (File.Exists manifestPath) then
        Error(
            sprintf
                "the conformance corpus is not at '%s'. It is a build input: clone the Fact wire specification beside this repository, or point %s at a clone."
                corpusDir
                SpecVariable
        )
    else
        use doc = JsonDocument.Parse(File.ReadAllBytes manifestPath)
        let root = doc.RootElement

        let vectors =
            root.GetProperty("vectors").EnumerateArray()
            |> Seq.map (fun v -> {
                Id = v.GetProperty("id").GetString()
                Family = v.GetProperty("family").GetString()
                Kind = v.GetProperty("kind").GetString()
                File = v.GetProperty("file").GetString()
                Sha256 = v.GetProperty("sha256").GetString()
                Digest = optString v "digest"
                RejectClass = optString v "rejectClass"
                SameAs = optString v "sameAs"
                LineageKey = optString v "lineageKey"
            })
            |> List.ofSeq

        let relations =
            root.GetProperty("relations").EnumerateArray()
            |> Seq.map (fun r -> {
                Ids = optString r "ids"
                History = optString r "history"
                Vectors =
                    r.GetProperty("vectors").EnumerateArray()
                    |> Seq.map (fun x -> x.GetString())
                    |> List.ofSeq
            })
            |> List.ofSeq

        let manifest = {
            Specification = root.GetProperty("specification").GetString()
            WireVersion = root.GetProperty("wireVersion").GetInt32()
            Vectors = vectors
            Relations = relations
        }

        Ok(manifest, (fun (file: string) -> File.ReadAllBytes(Path.Combine(corpusDir, file))))

/// The corpus beside this repository (or at `FUARAN_FACT_SPEC`).
let loadDefault () =
    load (Path.Combine(specRoot (), "wire-fixtures"))
