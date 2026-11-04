/// Certification against the Fact wire specification's conformance corpus (FACT_WIRE.md §9),
/// written once and run on both legs: by Expecto on .NET, and compiled to JavaScript by Fable and
/// run under node. Each leg supplies only what is irreducibly per-host — reading a file, and parsing
/// the manifest, which is an index for humans rather than a specified document.
module Fuaran.Facts.Tests.Certify

open Fuaran.Facts

/// One vector of `wire-fixtures/manifest.json`.
type Vector = {
    Id: string
    Family: string
    Kind: string
    File: string
    Sha256: string
    Digest: string option
    RejectClass: string option
    SameAs: string option
}

/// One entry of the manifest's `relations` (§9.3): `Ids` is `equal` or `distinct`, or `History` is
/// `extends` or `doesNotExtend`, over two vectors.
type Relation = {
    Ids: string option
    History: string option
    Vectors: string list
}

/// The manifest, as a leg's own JSON reader produced it.
type Manifest = {
    Specification: string
    WireVersion: int
    Vectors: Vector list
    Relations: Relation list
}

/// What a certification run did: each check's name and its failure, if any.
type Outcome = {
    Executed: int
    Listed: int
    RelationsChecked: int
    Failures: (string * string) list
}

let private sha256OfBytes (bytes: byte[]) = Fuaran.Core.Hash.sha256HexOfBytes bytes

let private bytesEqual (a: byte[]) (b: byte[]) =
    a.Length = b.Length && Array.forall2 (=) a b

/// Certifies one vector: `Ok ()`, or why it failed.
let certifyVector (readFile: string -> byte[]) (byId: Map<string, Vector>) (v: Vector) : Result<unit, string> =
    let bytes = readFile v.File

    if sha256OfBytes bytes <> v.Sha256 then
        Error(sprintf "the file's sha256 is %s, the manifest lists %s" (sha256OfBytes bytes) v.Sha256)
    else
        let roundTrip () =
            match FactWire.read bytes with
            | Error r -> Error("refused: " + Refusal.describe r)
            | Ok read ->
                let again = FactWire.utf8 (FactWire.encode read.Document)

                if bytesEqual again bytes then
                    Ok read
                else
                    Error "re-encoding does not reproduce the document's bytes"

        match v.Kind with
        | "round-trip" -> roundTrip () |> Result.map ignore
        | "hash" ->
            match roundTrip () with
            | Error e -> Error e
            | Ok read ->
                let digest =
                    match v.Family, read.Document with
                    | "identity", FactDocument f -> Some(Fact.computeFor f)
                    | ("definition" | "registry"), _ -> Some(FactWire.definitionDigest bytes)
                    | _ -> None

                match digest, v.Digest with
                | Some d, Some expected when d = expected -> Ok()
                | Some d, Some expected -> Error(sprintf "digest %s, the manifest lists %s" d expected)
                | None, _ -> Error(sprintf "no digest rule for family '%s'" v.Family)
                | _, None -> Error "the manifest lists no digest"
        | "reject" ->
            match FactWire.read bytes, v.RejectClass with
            | Ok _, _ -> Error "read without refusal"
            | Error r, Some expected when RefusalClass.code r.Class = expected -> Ok()
            | Error r, expected -> Error(sprintf "refused for %s, the manifest names %A" (Refusal.describe r) expected)
        | "accept" ->
            match FactWire.read bytes, v.SameAs |> Option.bind (fun s -> Map.tryFind s byId) with
            | Error r, _ -> Error("refused: " + Refusal.describe r)
            | Ok _, None -> Error "the vector names no readable sameAs"
            | Ok read, Some other ->
                match FactWire.read (readFile other.File) with
                | Error r -> Error("its sameAs is refused: " + Refusal.describe r)
                | Ok theirs ->
                    if List.isEmpty read.UnknownMembers then
                        Error "an accept vector carried no member this version does not know"
                    elif FactWire.encode read.Document = FactWire.encode theirs.Document then
                        Ok()
                    else
                        Error(sprintf "does not read as %s" other.Id)
        | k -> Error(sprintf "unknown vector kind '%s'" k)

let private factOf (readFile: string -> byte[]) (v: Vector) : Result<Fact, string> =
    match FactWire.read (readFile v.File) with
    | Ok { Document = FactDocument f } -> Ok f
    | Ok _ -> Error(v.Id + " is not a fact")
    | Error r -> Error(v.Id + ": " + Refusal.describe r)

let private historyOf (readFile: string -> byte[]) (v: Vector) : Result<Fact list, string> =
    match FactWire.read (readFile v.File) with
    | Ok { Document = FactHistoryDocument h } -> Ok h
    | Ok _ -> Error(v.Id + " is not a history")
    | Error r -> Error(v.Id + ": " + Refusal.describe r)

/// Checks one relation (§9.3).
let certifyRelation (readFile: string -> byte[]) (byId: Map<string, Vector>) (r: Relation) : Result<unit, string> =
    match r.Vectors |> List.map (fun id -> Map.tryFind id byId) with
    | [ Some a; Some b ] ->
        match r.Ids, r.History with
        | Some relation, None ->
            match factOf readFile a, factOf readFile b with
            | Ok fa, Ok fb ->
                let equal = Fact.computeFor fa = Fact.computeFor fb

                match relation with
                | "equal" when equal -> Ok()
                | "distinct" when not equal -> Ok()
                | _ -> Error(sprintf "ids are not %s" relation)
            | Error e, _
            | _, Error e -> Error e
        | None, Some relation ->
            match historyOf readFile a, historyOf readFile b with
            | Ok ha, Ok hb ->
                let extends = History.extends ha hb

                match relation with
                | "extends" when extends -> Ok()
                | "doesNotExtend" when not extends -> Ok()
                | _ -> Error(sprintf "the history relation is not %s" relation)
            | Error e, _
            | _, Error e -> Error e
        | _ -> Error "a relation names exactly one of ids and history"
    | _ -> Error(sprintf "a relation names two listed vectors, and these are %A" r.Vectors)

/// Runs the whole corpus: every vector, every relation, and the count check that a run which
/// executed nothing cannot pass (wire-fixtures/README.md, "Certifying", step 3).
let run (readFile: string -> byte[]) (manifest: Manifest) : Outcome =
    let byId = manifest.Vectors |> List.map (fun v -> v.Id, v) |> Map.ofList
    let failures = ResizeArray<string * string>()
    let mutable executed = 0

    if
        manifest.Specification <> "fact-wire"
        || manifest.WireVersion <> FactWire.WireVersion
    then
        failures.Add(
            ("manifest",
             sprintf
                 "certifies '%s' version %d, not fact-wire %d"
                 manifest.Specification
                 manifest.WireVersion
                 FactWire.WireVersion)
        )

    for v in manifest.Vectors do
        executed <- executed + 1

        let result =
            try
                certifyVector readFile byId v
            with e ->
                Error("threw: " + e.Message)

        match result with
        | Ok() -> ()
        | Error why -> failures.Add((v.Id, why))

    let mutable relations = 0

    for r in manifest.Relations do
        relations <- relations + 1

        match certifyRelation readFile byId r with
        | Ok() -> ()
        | Error why -> failures.Add(("relation " + String.concat " / " r.Vectors, why))

    if manifest.Vectors.Length = 0 then
        failures.Add(("manifest", "lists no vector: a run over it would pass having executed nothing"))

    if executed <> manifest.Vectors.Length then
        failures.Add(("manifest", sprintf "executed %d of %d vectors" executed manifest.Vectors.Length))

    {
        Executed = executed
        Listed = manifest.Vectors.Length
        RelationsChecked = relations
        Failures = List.ofSeq failures
    }
