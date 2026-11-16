namespace Fuaran.Facts

/// An as-of view (FACT_WIRE.md §6.4): the facts of `History` visible at `At`.
[<NoComparison>]
type AsOfView = {
    At: Instant
    History: Fact list
    /// The ids of `History.visibleAt History At`, ascending ordinally.
    Visible: string list
}

/// A fact's derived status (§6.6): forecastness and freshness under a staleness policy at an
/// instant, given whether the fact is current.
[<NoComparison>]
type FactStatus = {
    Fact: Fact
    Staleness: StalenessPolicy
    Current: bool
    At: Instant
    Forecast: bool
    Fresh: bool
}

/// One rendering, stated so that hosts can certify against it (§10.6): `Value` shown under `Metric` by
/// the rendering rule `Rule` is `Text`.
[<NoComparison>]
type RenderedValue = {
    Rule: int
    Metric: MetricDefinition option
    Value: FactValue
    Text: string
}

/// The nine document kinds (§9.6).
[<NoComparison>]
type FactDocument =
    | FactDocument of Fact
    | MetricDefinitionDocument of MetricDefinition
    | SubjectDefinitionDocument of SubjectDefinition
    | MetricRegistryDocument of MetricDefinition list
    | SubjectRegistryDocument of SubjectDefinition list
    | FactHistoryDocument of Fact list
    | AsOfViewDocument of AsOfView
    | FactStatusDocument of FactStatus
    | RenderedValueDocument of RenderedValue

/// A document as read: what it reads as, and the members it carried that this version does not
/// define (§3.2) — read leniently, and dropped on re-encoding.
[<NoComparison>]
type ReadDocument = {
    Document: FactDocument
    UnknownMembers: string list
}

/// The canonical codec (§3): reads a document's bytes, applying every rule and law the
/// specification puts on a reader, and writes the one canonical encoding of a document.
[<RequireQualifiedAccess>]
module FactWire =

    /// The wire version this library reads and writes.
    [<Literal>]
    let WireVersion = 1

    exception private Refused of Refusal

    let private refuse c at msg = raise (Refused(Refusal.make c at msg))

    let private ok (r: Result<'T, Refusal>) : 'T =
        match r with
        | Ok v -> v
        | Error e -> raise (Refused e)

    // ── reading ──────────────────────────────────────────────────────────────────────────────────

    type private Ctx(storedTokens: bool) =
        member val Unknown = ResizeArray<string>()

        /// Whether the token rules of §3.5 are waived: true only for a fact read from data at rest by
        /// `LegacyFact` (§3.5, the legacy reader's tolerance), never for a document.
        member _.StoredTokens = storedTokens

    let private obj (v: JsonValue) (at: string) : (string * JsonValue) list =
        match v with
        | JObject members -> members
        | _ -> refuse RefusalClass.ShapeInvalid at "expected an object"

    /// The member's value; the last one when a name repeats, as a JSON reader takes it.
    let private tryMember (members: (string * JsonValue) list) (name: string) : JsonValue option =
        members |> List.tryFindBack (fun (k, _) -> k = name) |> Option.map snd

    let private get (members: (string * JsonValue) list) (name: string) : JsonValue =
        match tryMember members name with
        | Some v -> v
        | None -> JNull

    /// Takes the declared members of a record, noting any member it does not declare.
    let private members (ctx: Ctx) (v: JsonValue) (at: string) (names: string list) =
        let m = obj v at

        for k, _ in m do
            if not (List.contains k names) && not (ctx.Unknown.Contains(at + "." + k)) then
                ctx.Unknown.Add(at + "." + k)

        for name in names do
            if (tryMember m name).IsNone then
                refuse RefusalClass.ShapeInvalid at (sprintf "member '%s' is missing" name)

        m

    let private str (v: JsonValue) (at: string) (nonEmpty: bool) : string =
        match v with
        | JString s ->
            if not (TextRules.isWellFormed s) then
                refuse RefusalClass.ShapeInvalid at "the string is not well-formed Unicode"
            elif nonEmpty && s.Length = 0 then
                refuse RefusalClass.ShapeInvalid at "must not be empty"
            else
                s
        | _ -> refuse RefusalClass.ShapeInvalid at "expected a string"

    let private optStr (v: JsonValue) (at: string) (nonEmpty: bool) : string option =
        match v with
        | JNull -> None
        | _ -> Some(str v at nonEmpty)

    let private isIntegerToken (t: string) =
        let body = if t.StartsWith "-" then t.Substring 1 else t

        body.Length > 0
        && body.Length <= 18
        && TextRules.allChars TextRules.isDigit body
        && (body.Length = 1 || body[0] <> '0')

    let private int' (v: JsonValue) (at: string) (lo: int64) (hi: int64) : int64 =
        match v with
        | JNumber t when isIntegerToken t ->
            let n = int64 t

            if n < lo || n > hi then
                refuse RefusalClass.ShapeInvalid at (sprintf "%s is outside %d..%d" t lo hi)

            n
        | _ -> refuse RefusalClass.ShapeInvalid at "expected an integer"

    let private bool' (v: JsonValue) (at: string) : bool =
        match v with
        | JBool b -> b
        | _ -> refuse RefusalClass.ShapeInvalid at "expected a boolean"

    let private arr (v: JsonValue) (at: string) : JsonValue list =
        match v with
        | JArray items -> items
        | _ -> refuse RefusalClass.ShapeInvalid at "expected an array"

    let private kindOf (v: JsonValue) (at: string) (known: string list) : string =
        let m = obj v at

        match tryMember m "kind" with
        | Some(JString k) ->
            if List.contains k known then
                k
            else
                refuse
                    RefusalClass.KindUnknown
                    (at + ".kind")
                    (sprintf "'%s' is not one of %s" k (String.concat ", " known))
        | _ -> refuse RefusalClass.ShapeInvalid (at + ".kind") "expected a string"

    let private head (v: JsonValue) (at: string) (kind: string) =
        let m = obj v at

        if get m "kind" <> JString kind then
            refuse RefusalClass.KindUnknown (at + ".kind") (sprintf "expected '%s'" kind)

        match tryMember m "wireVersion" with
        | None -> refuse RefusalClass.ShapeInvalid at "member 'wireVersion' is missing"
        | Some(JNumber t) when isIntegerToken t ->
            if t <> string WireVersion then
                refuse RefusalClass.VersionUnsupported (at + ".wireVersion") t
        | Some _ -> refuse RefusalClass.ShapeInvalid (at + ".wireVersion") "expected an integer"

    let private decimal' (v: JsonValue) (at: string) : DecimalText =
        match v with
        | JString s -> ok (DecimalText.tryParseAt at s)
        | _ -> refuse RefusalClass.ShapeInvalid at "a decimal is a string"

    let private instant (v: JsonValue) (at: string) : Instant =
        match v with
        | JString s -> ok (Instant.tryParseAt at s)
        | _ -> refuse RefusalClass.ShapeInvalid at "an instant is a string"

    /// Refuses (`token-invalid`) a token that contains one of the separators joined around it (§3.5),
    /// unless the fact is being read from data at rest.
    let private noSeparator (ctx: Ctx) (separators: string list) (at: string) (s: string) =
        for sep in separators do
            if not ctx.StoredTokens && s.Contains sep then
                refuse RefusalClass.TokenInvalid at (sprintf "'%s' contains '%s'" s sep)

    let private readSubject (ctx: Ctx) (v: JsonValue) (at: string) : SubjectRef =
        let m = members ctx v at [ "hierarchy"; "path" ]
        let hierarchy = str (get m "hierarchy") (at + ".hierarchy") true
        noSeparator ctx [ "/"; Fact.LineageSeparator ] (at + ".hierarchy") hierarchy

        let path =
            arr (get m "path") (at + ".path")
            |> List.mapi (fun i p ->
                let where = sprintf "%s.path[%d]" at i
                let s = str p where (not ctx.StoredTokens)
                noSeparator ctx [ ">"; Fact.LineageSeparator ] where s
                s)

        { Hierarchy = hierarchy; Path = path }

    let private readValue (ctx: Ctx) (v: JsonValue) (at: string) : FactValue =
        match kindOf v at [ "scalar"; "interval"; "series"; "distribution"; "categorical"; "absent" ] with
        | "scalar" ->
            let m = members ctx v at [ "kind"; "value" ]
            Scalar(decimal' (get m "value") (at + ".value"))
        | "interval" ->
            let m = members ctx v at [ "kind"; "low"; "high" ]
            let lo = decimal' (get m "low") (at + ".low")
            let hi = decimal' (get m "high") (at + ".high")

            if DecimalText.compare lo hi > 0 then
                refuse RefusalClass.IntervalInverted at (sprintf "low %s exceeds high %s" lo.Text hi.Text)

            Interval(lo, hi)
        | "series" ->
            let m = members ctx v at [ "kind"; "address"; "locator" ]
            let text = str (get m "address") (at + ".address") true

            match SeriesAddress.tryParse text with
            | Some address -> Series(address, optStr (get m "locator") (at + ".locator") true)
            | None ->
                refuse
                    RefusalClass.ShapeInvalid
                    (at + ".address")
                    (sprintf "'%s' is in a content-hash form but is not a sha256 content address" text)
        | "distribution" ->
            let m = members ctx v at [ "kind"; "buckets" ]
            let buckets = obj (get m "buckets") (at + ".buckets")

            if List.isEmpty buckets then
                refuse RefusalClass.ShapeInvalid (at + ".buckets") "a distribution has at least one bucket"

            buckets
            |> List.map (fun (k, x) ->
                let key = str (JString k) (at + ".buckets key") true

                if not ctx.StoredTokens && (key.Contains "=" || key.Contains ";") then
                    refuse RefusalClass.TokenInvalid (at + ".buckets") (sprintf "key '%s' contains '=' or ';'" key)

                key, decimal' x (at + ".buckets." + key))
            |> Map.ofList
            |> Distribution
        | "categorical" ->
            let m = members ctx v at [ "kind"; "value" ]
            Categorical(str (get m "value") (at + ".value") true)
        | _ ->
            let m = members ctx v at [ "kind"; "reason" ]
            Absent(str (get m "reason") (at + ".reason") true)

    let private readPeriod (ctx: Ctx) (v: JsonValue) (at: string) : TemporalExtent =
        let m = members ctx v at [ "from"; "to"; "label" ]
        let from = instant (get m "from") (at + ".from")
        let to' = instant (get m "to") (at + ".to")

        if from > to' then
            refuse
                RefusalClass.PeriodInverted
                at
                (sprintf "from %s is after to %s" (Instant.toWire from) (Instant.toWire to'))

        {
            From = from
            To = to'
            Label = optStr (get m "label") (at + ".label") true
        }

    let private readMethod (ctx: Ctx) (v: JsonValue) (at: string) : MethodRef =
        match kindOf v at [ "computed"; "humanAsserted"; "imported" ] with
        | "computed" ->
            let m = members ctx v at [ "kind"; "operation"; "version"; "paramHash" ]
            let operation = str (get m "operation") (at + ".operation") true
            let version = str (get m "version") (at + ".version") true

            for name, s in [ "operation", operation; "version", version ] do
                if not ctx.StoredTokens && s.Contains ":" then
                    refuse RefusalClass.TokenInvalid (at + "." + name) (sprintf "'%s' contains ':'" s)

            Computed(operation, version, str (get m "paramHash") (at + ".paramHash") false)
        | "humanAsserted" ->
            let m = members ctx v at [ "kind"; "principal" ]
            HumanAsserted(str (get m "principal") (at + ".principal") true)
        | _ ->
            let m = members ctx v at [ "kind"; "certificate" ]
            Imported(str (get m "certificate") (at + ".certificate") true)

    let private readEvidence (ctx: Ctx) (v: JsonValue) (at: string) : Evidence =
        let m = members ctx v at [ "resultRef"; "inputHashes"; "triggerRef" ]

        {
            ResultRef = optStr (get m "resultRef") (at + ".resultRef") true
            InputHashes =
                arr (get m "inputHashes") (at + ".inputHashes")
                |> List.mapi (fun i h -> str h (sprintf "%s.inputHashes[%d]" at i) true)
            TriggerRef = optStr (get m "triggerRef") (at + ".triggerRef") true
        }

    let private readConfidence (ctx: Ctx) (v: JsonValue) (at: string) : Confidence option =
        match v with
        | JNull -> None
        | _ ->
            let m = members ctx v at [ "value"; "basis" ]

            Some {
                Value = decimal' (get m "value") (at + ".value")
                Basis = optStr (get m "basis") (at + ".basis") true
            }

    let private readDisclosure (ctx: Ctx) (v: JsonValue) (at: string) : Disclosure =
        match kindOf v at [ "surfaceable"; "internal"; "restricted" ] with
        | "restricted" ->
            let m = members ctx v at [ "kind"; "policy" ]
            Restricted(str (get m "policy") (at + ".policy") true)
        | k ->
            members ctx v at [ "kind" ] |> ignore
            if k = "internal" then Internal else Surfaceable

    let private factMembers = [
        "kind"
        "wireVersion"
        "id"
        "subject"
        "metric"
        "value"
        "period"
        "asOf"
        "method"
        "evidence"
        "confidence"
        "supersedes"
        "disclosure"
    ]

    let private readFact (ctx: Ctx) (v: JsonValue) (at: string) : Fact =
        obj v at |> ignore
        head v at "fact"
        let m = members ctx v at factMembers
        let id = str (get m "id") (at + ".id") false

        if not (TextRules.isFactId id) then
            refuse RefusalClass.ShapeInvalid (at + ".id") (sprintf "'%s' is not 64 lower-case hex digits" id)

        let subject = readSubject ctx (get m "subject") (at + ".subject")
        let metric = str (get m "metric") (at + ".metric") true
        noSeparator ctx [ Fact.LineageSeparator ] (at + ".metric") metric
        let value = readValue ctx (get m "value") (at + ".value")
        let period = readPeriod ctx (get m "period") (at + ".period")
        let asOf = instant (get m "asOf") (at + ".asOf")
        let method = readMethod ctx (get m "method") (at + ".method")
        let evidence = readEvidence ctx (get m "evidence") (at + ".evidence")
        let confidence = readConfidence ctx (get m "confidence") (at + ".confidence")
        let supersedes = optStr (get m "supersedes") (at + ".supersedes") false
        let disclosure = readDisclosure ctx (get m "disclosure") (at + ".disclosure")

        match supersedes with
        | Some s when not (TextRules.isFactId s) ->
            refuse RefusalClass.ShapeInvalid (at + ".supersedes") (sprintf "'%s' is not a fact id" s)
        | _ -> ()

        let fact = {
            FactId = id
            Subject = subject
            Metric = MetricRef metric
            Value = value
            Period = period
            AsOf = asOf
            Method = method
            Evidence = evidence
            Confidence = confidence
            Supersedes = supersedes
            Disclosure = disclosure
        }

        let computed = Fact.computeFor fact

        if computed <> id then
            refuse RefusalClass.FactIdMismatch (at + ".id") (sprintf "states %s, the frame gives %s" id computed)

        fact

    let private readStaleness (ctx: Ctx) (v: JsonValue) (at: string) : StalenessPolicy =
        match kindOf v at [ "freshFor"; "untilSuperseded"; "untilUpstreamChange" ] with
        | "freshFor" ->
            let m = members ctx v at [ "kind"; "windowSeconds" ]
            FreshFor(int (int' (get m "windowSeconds") (at + ".windowSeconds") 1L 2147483647L))
        | k ->
            members ctx v at [ "kind" ] |> ignore

            if k = "untilSuperseded" then
                UntilSuperseded
            else
                UntilUpstreamChange

    let private readDefinitionId (v: JsonValue) (at: string) : string =
        let id = str v at true

        if not (TextRules.isDefinitionId id) then
            refuse RefusalClass.TokenInvalid at (sprintf "'%s' is not a definition id" id)

        id

    let private metricMembers = [
        "kind"
        "wireVersion"
        "id"
        "name"
        "unit"
        "dimensionality"
        "direction"
        "displayFormat"
        "staleness"
        "producingOperation"
        "canonicalMethod"
        "rollUp"
        "context"
    ]

    let private readMetricDefinition (ctx: Ctx) (v: JsonValue) (at: string) : MetricDefinition =
        obj v at |> ignore
        head v at "metricDefinition"
        let m = members ctx v at metricMembers
        let id = readDefinitionId (get m "id") (at + ".id")
        let name = str (get m "name") (at + ".name") true
        let unitText = str (get m "unit") (at + ".unit") false
        let dimensionality = str (get m "dimensionality") (at + ".dimensionality") true
        let directionText = str (get m "direction") (at + ".direction") false

        let producingOperation =
            optStr (get m "producingOperation") (at + ".producingOperation") true

        let canonicalMethod =
            optStr (get m "canonicalMethod") (at + ".canonicalMethod") true

        let context = optStr (get m "context") (at + ".context") true

        let direction =
            match directionText with
            | "higherIsBetter" -> HigherIsBetter
            | "lowerIsBetter" -> LowerIsBetter
            | "neutral" -> Neutral
            | d -> refuse RefusalClass.ShapeInvalid (at + ".direction") (sprintf "'%s'" d)

        let unit = ok (MetricUnit.tryParseAt (at + ".unit") unitText)
        let fmtAt = at + ".displayFormat"

        let fm =
            members ctx (get m "displayFormat") fmtAt [ "style"; "fractionDigits"; "grouping" ]

        let style =
            match str (get fm "style") (fmtAt + ".style") false with
            | "verbatim" -> Verbatim
            | "number" -> Number
            | "percent" -> Percent
            | "currency" -> Currency
            | s -> refuse RefusalClass.ShapeInvalid (fmtAt + ".style") (sprintf "'%s'" s)

        let fractionDigits =
            match get fm "fractionDigits" with
            | JNull -> None
            | x -> Some(int (int' x (fmtAt + ".fractionDigits") 0L 28L))

        let format = {
            Style = style
            FractionDigits = fractionDigits
            Grouping = bool' (get fm "grouping") (fmtAt + ".grouping")
        }

        ok (DisplayFormat.checkAt fmtAt unit format)
        let staleness = readStaleness ctx (get m "staleness") (at + ".staleness")

        let rollUp =
            match get m "rollUp" with
            | JNull -> None
            | r ->
                match kindOf r (at + ".rollUp") [ "additive"; "nonAdditive" ] with
                | "additive" ->
                    let rm = members ctx r (at + ".rollUp") [ "kind"; "tolerance" ]
                    let t = decimal' (get rm "tolerance") (at + ".rollUp.tolerance")

                    if DecimalText.isNegative t then
                        refuse RefusalClass.DecimalInvalid (at + ".rollUp.tolerance") "a tolerance is not negative"

                    Some(Additive t)
                | _ ->
                    members ctx r (at + ".rollUp") [ "kind" ] |> ignore
                    Some NonAdditive

        {
            Id = id
            Name = name
            Unit = unit
            Dimensionality = dimensionality
            Direction = direction
            DisplayFormat = format
            Staleness = staleness
            ProducingOperation = producingOperation
            CanonicalMethod = canonicalMethod
            RollUp = rollUp
            Context = context
            RecomputePolicy = None
        }

    let private readSubjectDefinition (ctx: Ctx) (v: JsonValue) (at: string) : SubjectDefinition =
        obj v at |> ignore
        head v at "subjectDefinition"

        let m =
            members ctx v at [ "kind"; "wireVersion"; "id"; "name"; "levels"; "calendar" ]

        let levels =
            arr (get m "levels") (at + ".levels")
            |> List.mapi (fun i l -> str l (sprintf "%s.levels[%d]" at i) true)

        if List.isEmpty levels then
            refuse RefusalClass.ShapeInvalid (at + ".levels") "a hierarchy has at least one level"

        {
            Id = readDefinitionId (get m "id") (at + ".id")
            Name = str (get m "name") (at + ".name") true
            Levels = levels
            Calendar = optStr (get m "calendar") (at + ".calendar") true
        }

    let private readRegistry
        (ctx: Ctx)
        (v: JsonValue)
        (at: string)
        (kind: string)
        (memberName: string)
        (readOne: Ctx -> JsonValue -> string -> 'T)
        (idOf: 'T -> string)
        : 'T list =
        obj v at |> ignore
        head v at kind
        let m = members ctx v at [ "kind"; "wireVersion"; memberName ]

        let items =
            arr (get m memberName) (at + "." + memberName)
            |> List.mapi (fun i x -> readOne ctx x (sprintf "%s.%s[%d]" at memberName i))

        items
        |> List.pairwise
        |> List.iter (fun (a, b) ->
            if Ordinal.compare (idOf a) (idOf b) >= 0 then
                refuse
                    RefusalClass.RegistryUnordered
                    (at + "." + memberName)
                    (sprintf "'%s' then '%s': ids are unique and ascend ordinally" (idOf a) (idOf b)))

        items

    let private readHistory (ctx: Ctx) (v: JsonValue) (at: string) : Fact list =
        obj v at |> ignore
        head v at "factHistory"
        let m = members ctx v at [ "kind"; "wireVersion"; "facts" ]

        let facts =
            arr (get m "facts") (at + ".facts")
            |> List.mapi (fun i f -> readFact ctx f (sprintf "%s.facts[%d]" at i))

        ok (History.checkAt at facts)
        facts

    // ── writing ──────────────────────────────────────────────────────────────────────────────────

    let private optString (s: string option) =
        match s with
        | Some v -> JString v
        | None -> JNull

    let private valueTree (value: FactValue) : JsonValue =
        match value with
        | Scalar d -> JObject [ "kind", JString "scalar"; "value", JString d.Text ]
        | Interval(lo, hi) -> JObject [ "kind", JString "interval"; "low", JString lo.Text; "high", JString hi.Text ]
        | Series(address, locator) ->
            JObject [
                "kind", JString "series"
                "address", JString address.Text
                "locator", optString locator
            ]
        | Distribution buckets ->
            JObject [
                "kind", JString "distribution"
                "buckets", JMap(buckets |> Map.toList |> List.map (fun (k, d) -> k, JString d.Text))
            ]
        | Categorical c -> JObject [ "kind", JString "categorical"; "value", JString c ]
        | Absent reason -> JObject [ "kind", JString "absent"; "reason", JString reason ]

    let private factTree (f: Fact) : JsonValue =
        let value = valueTree f.Value

        let method =
            match f.Method with
            | Computed(op, version, paramHash) ->
                JObject [
                    "kind", JString "computed"
                    "operation", JString op
                    "version", JString version
                    "paramHash", JString paramHash
                ]
            | HumanAsserted principal -> JObject [ "kind", JString "humanAsserted"; "principal", JString principal ]
            | Imported certificate -> JObject [ "kind", JString "imported"; "certificate", JString certificate ]

        let disclosure =
            match f.Disclosure with
            | Surfaceable -> JObject [ "kind", JString "surfaceable" ]
            | Internal -> JObject [ "kind", JString "internal" ]
            | Restricted policy -> JObject [ "kind", JString "restricted"; "policy", JString policy ]

        JObject [
            "kind", JString "fact"
            "wireVersion", JNumber(string WireVersion)
            "id", JString f.FactId
            "subject",
            JObject [
                "hierarchy", JString f.Subject.Hierarchy
                "path", JArray(f.Subject.Path |> List.map JString)
            ]
            "metric", JString f.Metric.Value
            "value", value
            "period",
            JObject [
                "from", JString(Instant.toWire f.Period.From)
                "to", JString(Instant.toWire f.Period.To)
                "label", optString f.Period.Label
            ]
            "asOf", JString(Instant.toWire f.AsOf)
            "method", method
            "evidence",
            JObject [
                "resultRef", optString f.Evidence.ResultRef
                "inputHashes", JArray(f.Evidence.InputHashes |> List.map JString)
                "triggerRef", optString f.Evidence.TriggerRef
            ]
            "confidence",
            (match f.Confidence with
             | None -> JNull
             | Some c -> JObject [ "value", JString c.Value.Text; "basis", optString c.Basis ])
            "supersedes", optString f.Supersedes
            "disclosure", disclosure
        ]

    let private stalenessTree (s: StalenessPolicy) : JsonValue =
        match s with
        | FreshFor w -> JObject [ "kind", JString "freshFor"; "windowSeconds", JNumber(string w) ]
        | UntilSuperseded -> JObject [ "kind", JString "untilSuperseded" ]
        | UntilUpstreamChange -> JObject [ "kind", JString "untilUpstreamChange" ]

    let private metricDefinitionTree (d: MetricDefinition) : JsonValue =
        JObject [
            "kind", JString "metricDefinition"
            "wireVersion", JNumber(string WireVersion)
            "id", JString d.Id
            "name", JString d.Name
            "unit", JString(MetricUnit.text d.Unit)
            "dimensionality", JString d.Dimensionality
            "direction",
            JString(
                match d.Direction with
                | HigherIsBetter -> "higherIsBetter"
                | LowerIsBetter -> "lowerIsBetter"
                | Neutral -> "neutral"
            )
            "displayFormat",
            JObject [
                "style",
                JString(
                    match d.DisplayFormat.Style with
                    | Verbatim -> "verbatim"
                    | Number -> "number"
                    | Percent -> "percent"
                    | Currency -> "currency"
                )
                "fractionDigits",
                (match d.DisplayFormat.FractionDigits with
                 | Some n -> JNumber(string n)
                 | None -> JNull)
                "grouping", JBool d.DisplayFormat.Grouping
            ]
            "staleness", stalenessTree d.Staleness
            "producingOperation", optString d.ProducingOperation
            "canonicalMethod", optString d.CanonicalMethod
            "rollUp",
            (match d.RollUp with
             | None -> JNull
             | Some(Additive t) -> JObject [ "kind", JString "additive"; "tolerance", JString t.Text ]
             | Some NonAdditive -> JObject [ "kind", JString "nonAdditive" ])
            "context", optString d.Context
        ]

    let private subjectDefinitionTree (d: SubjectDefinition) : JsonValue =
        JObject [
            "kind", JString "subjectDefinition"
            "wireVersion", JNumber(string WireVersion)
            "id", JString d.Id
            "name", JString d.Name
            "levels", JArray(d.Levels |> List.map JString)
            "calendar", optString d.Calendar
        ]

    let private historyTree (facts: Fact list) : JsonValue =
        JObject [
            "kind", JString "factHistory"
            "wireVersion", JNumber(string WireVersion)
            "facts", JArray(facts |> List.map factTree)
        ]

    let private documentTree (doc: FactDocument) : JsonValue =
        match doc with
        | FactDocument f -> factTree f
        | MetricDefinitionDocument d -> metricDefinitionTree d
        | SubjectDefinitionDocument d -> subjectDefinitionTree d
        | MetricRegistryDocument ds ->
            JObject [
                "kind", JString "metricRegistry"
                "wireVersion", JNumber(string WireVersion)
                "metrics", JArray(ds |> List.map metricDefinitionTree)
            ]
        | SubjectRegistryDocument ds ->
            JObject [
                "kind", JString "subjectRegistry"
                "wireVersion", JNumber(string WireVersion)
                "subjects", JArray(ds |> List.map subjectDefinitionTree)
            ]
        | FactHistoryDocument facts -> historyTree facts
        | AsOfViewDocument v ->
            JObject [
                "kind", JString "asOfView"
                "wireVersion", JNumber(string WireVersion)
                "at", JString(Instant.toWire v.At)
                "history", historyTree v.History
                "visible", JArray(v.Visible |> List.map JString)
            ]
        | FactStatusDocument s ->
            JObject [
                "kind", JString "factStatus"
                "wireVersion", JNumber(string WireVersion)
                "fact", factTree s.Fact
                "staleness", stalenessTree s.Staleness
                "current", JBool s.Current
                "at", JString(Instant.toWire s.At)
                "forecast", JBool s.Forecast
                "fresh", JBool s.Fresh
            ]
        | RenderedValueDocument r ->
            JObject [
                "kind", JString "renderedValue"
                "wireVersion", JNumber(string WireVersion)
                "rule", JNumber(string r.Rule)
                "metric",
                (match r.Metric with
                 | Some m -> metricDefinitionTree m
                 | None -> JNull)
                "value", valueTree r.Value
                "text", JString r.Text
            ]

    /// The canonical encoding of `doc` (§3), unchecked: a document built in code is written as it
    /// is. `write` is the checked form.
    let encode (doc: FactDocument) : string = Json.write (documentTree doc)

    // ── the document reader ──────────────────────────────────────────────────────────────────────

    let private documentKinds = [
        "fact"
        "metricDefinition"
        "subjectDefinition"
        "metricRegistry"
        "subjectRegistry"
        "factHistory"
        "asOfView"
        "factStatus"
        "renderedValue"
    ]

    let private readParsed (text: string) (raw: JsonValue) : ReadDocument =
        let ctx = Ctx(false)

        let doc =
            match kindOf raw "$" documentKinds with
            | "fact" -> FactDocument(readFact ctx raw "$")
            | "metricDefinition" -> MetricDefinitionDocument(readMetricDefinition ctx raw "$")
            | "subjectDefinition" -> SubjectDefinitionDocument(readSubjectDefinition ctx raw "$")
            | "metricRegistry" ->
                MetricRegistryDocument(
                    readRegistry ctx raw "$" "metricRegistry" "metrics" readMetricDefinition (fun d -> d.Id)
                )
            | "subjectRegistry" ->
                SubjectRegistryDocument(
                    readRegistry ctx raw "$" "subjectRegistry" "subjects" readSubjectDefinition (fun d -> d.Id)
                )
            | "factHistory" -> FactHistoryDocument(readHistory ctx raw "$")
            | "asOfView" ->
                head raw "$" "asOfView"
                let m = members ctx raw "$" [ "kind"; "wireVersion"; "at"; "history"; "visible" ]
                let at = instant (get m "at") "$.at"
                let history = readHistory ctx (get m "history") "$.history"

                let visible =
                    arr (get m "visible") "$.visible"
                    |> List.mapi (fun i x -> str x (sprintf "$.visible[%d]" i) false)

                let derived = History.visibleAt history at

                if derived <> visible then
                    refuse
                        RefusalClass.ViewMismatch
                        "$.visible"
                        (sprintf
                            "states [%s], the history gives [%s] at %s"
                            (String.concat ", " visible)
                            (String.concat ", " derived)
                            (Instant.toWire at))

                AsOfViewDocument {
                    At = at
                    History = history
                    Visible = visible
                }
            | "renderedValue" ->
                head raw "$" "renderedValue"

                let m =
                    members ctx raw "$" [ "kind"; "wireVersion"; "rule"; "metric"; "value"; "text" ]

                let rule = int (int' (get m "rule") "$.rule" -2147483648L 2147483647L)

                if rule <> FactRendering.Rule then
                    refuse RefusalClass.VersionUnsupported "$.rule" (sprintf "rendering rule %d" rule)

                let metric =
                    match get m "metric" with
                    | JNull -> None
                    | x -> Some(readMetricDefinition ctx x "$.metric")

                let value = readValue ctx (get m "value") "$.value"
                let text' = str (get m "text") "$.text" false
                let derived = FactRendering.renderFor metric value

                if text' <> derived then
                    refuse
                        RefusalClass.RenderingMismatch
                        "$.text"
                        (sprintf "states '%s', rule %d gives '%s'" text' rule derived)

                RenderedValueDocument {
                    Rule = rule
                    Metric = metric
                    Value = value
                    Text = text'
                }
            | _ ->
                head raw "$" "factStatus"

                let m =
                    members ctx raw "$" [
                        "kind"
                        "wireVersion"
                        "fact"
                        "staleness"
                        "current"
                        "at"
                        "forecast"
                        "fresh"
                    ]

                let fact = readFact ctx (get m "fact") "$.fact"
                let staleness = readStaleness ctx (get m "staleness") "$.staleness"
                let current = bool' (get m "current") "$.current"
                let at = instant (get m "at") "$.at"
                let forecast = bool' (get m "forecast") "$.forecast"
                let fresh = bool' (get m "fresh") "$.fresh"

                if forecast <> Fact.isForecast fact then
                    refuse
                        RefusalClass.StatusMismatch
                        "$.forecast"
                        (sprintf "states %b, the fact gives %b" forecast (Fact.isForecast fact))

                let derivedFresh = Freshness.isFresh staleness fact current at

                if fresh <> derivedFresh then
                    refuse
                        RefusalClass.StatusMismatch
                        "$.fresh"
                        (sprintf "states %b, the policy gives %b" fresh derivedFresh)

                FactStatusDocument {
                    Fact = fact
                    Staleness = staleness
                    Current = current
                    At = at
                    Forecast = forecast
                    Fresh = fresh
                }

        if ctx.Unknown.Count = 0 && encode doc <> text then
            refuse RefusalClass.NotCanonical "$" "the bytes are not the canonical encoding of the document they read as"

        {
            Document = doc
            UnknownMembers = List.ofSeq ctx.Unknown
        }

    /// Reads a document's text, applying every rule and law a reader applies, or refuses it for one
    /// named class.
    let readText (text: string) : Result<ReadDocument, Refusal> =
        try
            if text.Length > 0 && text[0] = '﻿' then
                refuse RefusalClass.NotCanonical "$" "a byte-order mark"

            match Json.parse text with
            | Error why -> refuse RefusalClass.ShapeInvalid "$" ("not JSON: " + why)
            | Ok raw -> Ok(readParsed text raw)
        with Refused r ->
            Error r

    /// Reads a document's bytes (UTF-8, no byte-order mark), or refuses it.
    let read (bytes: byte[]) : Result<ReadDocument, Refusal> =
        if bytes.Length >= 3 && bytes[0] = 0xEFuy && bytes[1] = 0xBBuy && bytes[2] = 0xBFuy then
            Error(Refusal.make RefusalClass.NotCanonical "$" "a byte-order mark")
        else
            match Json.decodeUtf8 bytes with
            | None -> Error(Refusal.make RefusalClass.NotCanonical "$" "the bytes are not UTF-8")
            | Some text -> readText text

    /// The canonical text of `doc`, checked: the text is read back under every rule a reader
    /// applies, so what this writes is what a conformant reader accepts. A fact with a stale id, a
    /// history that breaks a law, or a definition outside its grammar is refused here, at the writer.
    let write (doc: FactDocument) : Result<string, Refusal> =
        let text = encode doc

        match readText text with
        | Ok _ -> Ok text
        | Error r -> Error r

    /// `write`, as UTF-8 bytes.
    let writeBytes (doc: FactDocument) : Result<byte[], Refusal> = write doc |> Result.map Json.utf8Bytes

    /// The UTF-8 bytes of document text.
    let utf8 (text: string) : byte[] = Json.utf8Bytes text

    /// The definition digest (§7.5) of a definition or registry document's canonical bytes:
    /// `sha256:` and 64 lower-case hex digits.
    let definitionDigest (bytes: byte[]) : string =
        "sha256:" + TextRules.sha256HexOfBytes bytes

    /// The definition digest of a metric definition: two hosts agree on a metric exactly when their
    /// definitions of its id have one digest.
    let metricDigest (d: MetricDefinition) : Result<string, Refusal> =
        writeBytes (MetricDefinitionDocument d) |> Result.map definitionDigest

    /// The definition digest of a subject definition.
    let subjectDigest (d: SubjectDefinition) : Result<string, Refusal> =
        writeBytes (SubjectDefinitionDocument d) |> Result.map definitionDigest

    /// Reads one fact document.
    let readFactText (text: string) : Result<Fact, Refusal> =
        match readText text with
        | Ok { Document = FactDocument f } -> Ok f
        | Ok _ -> Error(Refusal.make RefusalClass.KindUnknown "$.kind" "expected 'fact'")
        | Error r -> Error r

    /// The canonical text of one fact, checked.
    let writeFact (f: Fact) : Result<string, Refusal> = write (FactDocument f)

    /// Checks a fact read from data at rest under every rule a reader applies to a fact, its id
    /// verified, except the token rules (§3.5, the legacy reader's tolerance): a hierarchy, path member,
    /// metric id, computed method or distribution key that a store already holds is read as it is.
    /// Writing such a fact is still refused, by `write`.
    let internal checkStored (f: Fact) : Result<Fact, Refusal> =
        try
            match Json.parse (encode (FactDocument f)) with
            | Error why -> refuse RefusalClass.ShapeInvalid "$" ("not JSON: " + why)
            | Ok raw -> Ok(readFact (Ctx(true)) raw "$")
        with Refused r ->
            Error r
