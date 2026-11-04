namespace Fuaran.Facts

/// The identity frame (FACT_WIRE.md §4.1): each string written as `<length>:<string>`, the length
/// in UTF-16 code units, concatenated, encoded as UTF-8 and hashed with SHA-256 to 64 lower-case hex
/// digits. A length prefix cannot be imitated by a field's content, so two different lists never
/// share a frame. This is the Fact frame, distinct from `Fuaran.Core`'s content address (§4.8).
[<RequireQualifiedAccess>]
module Frame =

    /// The framed text of `fields`, in order: `text ["ab"; "c"]` is `2:ab1:c`.
    let text (fields: string list) : string =
        let builder = System.Text.StringBuilder()

        for f in fields do
            // A .NET and a JavaScript string are both sequences of UTF-16 code units, so `Length`
            // is the count §4.1 asks for on every host this compiles to.
            builder.Append(string f.Length).Append(':').Append(f) |> ignore

        builder.ToString()

    /// The digest of `fields`: lower-case hex SHA-256 over the UTF-8 bytes of the frame.
    let digest (fields: string list) : string = TextRules.sha256Hex (text fields)

/// The content-hash form an input takes in a fact's evidence (§4.5): `<algorithm>:<hex>`, a
/// non-empty algorithm name of lower-case letters and digits, one colon, and an even number of
/// lower-case hex digits, at least 32. A reader tells the inputs that name data apart from other
/// entries in the list (a run token, a value hash) by this form alone.
[<RequireQualifiedAccess>]
module InputHash =

    /// The fewest hex digits an input hash carries: a 128-bit digest.
    [<Literal>]
    let MinimumDigits = 32

    /// Whether `value` is in the `<algorithm>:<hex>` form.
    let isContentHash (value: string) : bool =
        if isNull value then
            false
        else
            match value.IndexOf ':' with
            | colon when colon > 0 ->
                let algorithm = value.Substring(0, colon)
                let digest = value.Substring(colon + 1)

                TextRules.allChars (fun c -> (c >= 'a' && c <= 'z') || TextRules.isDigit c) algorithm
                && digest.Length >= MinimumDigits
                && digest.Length % 2 = 0
                && TextRules.allChars TextRules.isLowerHex digest
            | _ -> false

    /// The inputs an evidence names: its entries in the content-hash form, in the evidence's order.
    let named (evidence: Evidence) : string list =
        evidence.InputHashes |> List.filter isContentHash

/// A fact's identity, its lineage key and its forecastness (§4, §6.1, §6.5).
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Fact =

    /// The first field of every fact's frame: names the recipe, so a fact id never equals another
    /// kind of id over the same values.
    [<Literal>]
    let IdLabel = "fact"

    /// The subject rendering (§4.3): `hierarchy/` and the path joined by `>`.
    let subjectRendering (s: SubjectRef) : string =
        s.Hierarchy + "/" + String.concat ">" s.Path

    /// The period rendering (§4.3): each end's identity rendering, joined by `..`. The label is not
    /// in it.
    let periodRendering (p: TemporalExtent) : string =
        Instant.toIdentity p.From + ".." + Instant.toIdentity p.To

    /// The method identity (§4.3): `computed:op:version:paramHash`, `asserted:principal` or
    /// `imported:certificate`.
    let methodIdentity (m: MethodRef) : string =
        match m with
        | Computed(op, version, paramHash) -> "computed:" + op + ":" + version + ":" + paramHash
        | HumanAsserted principal -> "asserted:" + principal
        | Imported certificateRef -> "imported:" + certificateRef

    /// A value's value text (§4.4): what a person's assertion hashes.
    let valueText (v: FactValue) : string =
        match v with
        | Scalar d -> "scalar:" + d.Text
        | Interval(lo, hi) -> "interval:" + lo.Text + ":" + hi.Text
        | Series(address, _) -> "series:" + address
        | Distribution buckets ->
            buckets
            |> Map.toList
            |> List.sortWith (fun (a, _) (b, _) -> Ordinal.compare a b)
            |> List.map (fun (k, d) -> k + "=" + d.Text)
            |> String.concat ";"
            |> fun body -> "dist:" + body
        | Categorical c -> "cat:" + c
        | Absent reason -> "absent:" + reason

    /// The value hash (§4.4): lower-case hex SHA-256 of the value text, no prefix.
    let valueHash (v: FactValue) : string = TextRules.sha256Hex (valueText v)

    /// The inputs that enter the id (§4.4): for a `HumanAsserted` fact, the value hash followed by
    /// the evidence's inputs; for every other method, the evidence's inputs as carried.
    let effectiveInputHashes (method: MethodRef) (evidence: Evidence) (value: FactValue) : string list =
        match method with
        | HumanAsserted _ -> valueHash value :: evidence.InputHashes
        | Computed _
        | Imported _ -> evidence.InputHashes

    /// The identity fields (§4.2), in frame order: the label, the subject, the metric, the period,
    /// the method, then the inputs sorted ordinally with duplicates kept.
    let identityFields
        (subject: SubjectRef)
        (metric: MetricRef)
        (period: TemporalExtent)
        (method: MethodRef)
        (inputHashes: string list)
        : string list =
        [
            IdLabel
            subjectRendering subject
            metric.Value
            periodRendering period
            methodIdentity method
            yield! Ordinal.sort inputHashes
        ]

    /// The fact id of a (subject, metric, period, method, effective inputs) tuple — the digest of its
    /// frame. Asserting an identical tuple always yields the same id, so a store converges under
    /// replay and duplication.
    let compute
        (subject: SubjectRef)
        (metric: MetricRef)
        (period: TemporalExtent)
        (method: MethodRef)
        (inputHashes: string list)
        : string =
        Frame.digest (identityFields subject metric period method inputHashes)

    /// The id of a fully populated fact, recomputed from its own members — what a reader verifies
    /// an incoming fact's `FactId` against, rather than trusting it.
    let computeFor (f: Fact) : string =
        compute f.Subject f.Metric f.Period f.Method (effectiveInputHashes f.Method f.Evidence f.Value)

    /// The lineage key (§6.1): the subject, metric, period and method renderings, framed so the key
    /// is unambiguous. The inputs and the value are not in it: a new input or value supersedes within
    /// a lineage, while another method starts a competing one.
    let lineageKey (f: Fact) : string =
        Frame.text [
            subjectRendering f.Subject
            f.Metric.Value
            periodRendering f.Period
            methodIdentity f.Method
        ]

    /// Whether two facts are in one lineage.
    let sameLineage (a: Fact) (b: Fact) : bool = lineageKey a = lineageKey b

    /// Whether a fact is a forecast (§6.5): its valid time runs past the moment it was known.
    let isForecast (f: Fact) : bool = f.Period.To > f.AsOf
