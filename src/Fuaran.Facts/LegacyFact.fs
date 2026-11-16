namespace Fuaran.Facts

/// A reader for the **legacy saved shape**: a fact as an earlier generation of the kernel persisted it
/// through a general-purpose JSON serialiser, before the Fact wire specification existed. A store
/// holding facts in that shape adopts this package without rewriting them (`DECISIONS.md` D13).
///
/// The shape, as that serialiser writes it:
///
///   - a record's members are PascalCase (`FactId`, `Subject`, `AsOf`, …), in any order;
///   - a quantity is a JSON **number**, whose token text is the decimal text (`1234.50`);
///   - an instant is an ISO 8601 date and time with seven fractional digits, with or without a trailing
///     `Z` (`2026-04-02T06:30:00.5000000Z`, `2026-01-01T00:00:00.0000000`), and is UTC either way;
///   - a union case is an object with one member named for the case (`{"Scalar":1234.5}`,
///     `{"Interval":[1.0,2.50]}`, `{"Computed":["op","1","h"]}`), except a case with no fields, which is
///     its name as a string (`"Surfaceable"`);
///   - the metric is `{"MetricRef":"<id>"}`, and a series names its store's own version key, which this
///     package reads as a `HostKey` (or a `ContentAddress` when it is one).
///
/// The legacy shape is **read, never written**: `FactWire` is the only writer. What is read is then held
/// to every rule a reader of the specification applies to a fact — through `FactWire`'s own reader, so
/// there is one reader — so a legacy fact whose id is not the digest of its own members is refused here
/// exactly as the wire would refuse it.
///
/// One rule is waived, and only here: the token rules of FACT_WIRE.md §3.5. A store holds facts written
/// before the specification refused a separator inside a token, and they read back as stored, with
/// their id and lineage key unchanged (`DECISIONS.md` D14): a path member containing `>` or `|`, or
/// empty; a hierarchy containing `/` or `|`; a metric id containing `|`; a computed method's operation
/// or version containing `:`; a distribution key containing `=` or `;`. Such a fact cannot be written:
/// `FactWire.write` refuses it, so a new fact never carries one. Some of these tokens make two stored
/// facts render alike — the same lineage key, and for some the same id; each is still returned as
/// stored, and §3.5 says what a host does about it.
[<RequireQualifiedAccess>]
module LegacyFact =

    exception private Refused of Refusal

    let private refuse c at msg = raise (Refused(Refusal.make c at msg))

    let private ok (r: Result<'T, Refusal>) : 'T =
        match r with
        | Ok v -> v
        | Error e -> raise (Refused e)

    let private obj (v: JsonValue) (at: string) : (string * JsonValue) list =
        match v with
        | JObject members -> members
        | _ -> refuse RefusalClass.ShapeInvalid at "expected an object"

    /// A member that may be absent or `null`: both read as `JNull`.
    let private opt (members: (string * JsonValue) list) (name: string) : JsonValue =
        match members |> List.tryFindBack (fun (k, _) -> k = name) with
        | Some(_, v) -> v
        | None -> JNull

    let private get (members: (string * JsonValue) list) (name: string) (at: string) : JsonValue =
        match opt members name with
        | JNull -> refuse RefusalClass.ShapeInvalid at (sprintf "member '%s' is missing" name)
        | v -> v

    let private str (v: JsonValue) (at: string) : string =
        match v with
        | JString s -> s
        | _ -> refuse RefusalClass.ShapeInvalid at "expected a string"

    let private optStr (v: JsonValue) (at: string) : string option =
        match v with
        | JNull -> None
        | _ -> Some(str v at)

    let private arr (v: JsonValue) (at: string) : JsonValue list =
        match v with
        | JArray items -> items
        | _ -> refuse RefusalClass.ShapeInvalid at "expected an array"

    /// A quantity: a JSON number whose token text is decimal text (§3.3), kept exactly as written.
    let private quantity (v: JsonValue) (at: string) : DecimalText =
        match v with
        | JNumber t -> ok (DecimalText.tryParseAt at t)
        | _ -> refuse RefusalClass.ShapeInvalid at "a quantity in the legacy shape is a JSON number"

    /// An instant: `YYYY-MM-DDTHH:MM:SS[.fffffff][Z]`, UTC with or without the `Z`. An offset is refused,
    /// because the legacy shape never wrote one and its clock face would not be UTC.
    let private instant (v: JsonValue) (at: string) : Instant =
        let text = str v at

        let body =
            if text.EndsWith "Z" then
                text.Substring(0, text.Length - 1)
            else
                text

        if
            body.Length < 19
            || body.Substring(19).Contains "+"
            || body.Substring(19).Contains "-"
        then
            refuse RefusalClass.InstantInvalid at (sprintf "'%s' is not a UTC date and time" text)

        let wire =
            match body.IndexOf '.' with
            | -1 -> body
            | i ->
                let fraction = body.Substring(i + 1).TrimEnd '0'

                if fraction = "" then
                    body.Substring(0, i)
                else
                    body.Substring(0, i + 1) + fraction

        ok (Instant.tryParseAt at (wire + "Z"))

    /// A union case: its name and its fields' value (`JNull` for a case with no fields).
    let private case (v: JsonValue) (at: string) : string * JsonValue =
        match v with
        | JString name -> name, JNull
        | JObject [ name, fields ] -> name, fields
        | _ -> refuse RefusalClass.ShapeInvalid at "a union case is its name, or an object with one member"

    let private unknown (at: string) (name: string) (known: string list) =
        refuse RefusalClass.KindUnknown at (sprintf "'%s' is not one of %s" name (String.concat ", " known))

    let private readValue (v: JsonValue) (at: string) : FactValue =
        match case v at with
        | "Scalar", x -> Scalar(quantity x (at + ".Scalar"))
        | "Interval", x ->
            match arr x (at + ".Interval") with
            | [ lo; hi ] -> Interval(quantity lo (at + ".Interval[0]"), quantity hi (at + ".Interval[1]"))
            | _ -> refuse RefusalClass.ShapeInvalid (at + ".Interval") "an interval is two quantities"
        | "Series", x ->
            let key = str x (at + ".Series")

            match SeriesAddress.tryParse key with
            | Some address -> Series(address, None)
            | None ->
                refuse
                    RefusalClass.ShapeInvalid
                    (at + ".Series")
                    (sprintf "'%s' is in a content-hash form but is not a sha256 content address" key)
        | "Distribution", x ->
            obj x (at + ".Distribution")
            |> List.map (fun (k, q) -> k, quantity q (sprintf "%s.Distribution.%s" at k))
            |> Map.ofList
            |> Distribution
        | "Categorical", x -> Categorical(str x (at + ".Categorical"))
        | "Absent", x -> Absent(str x (at + ".Absent"))
        | name, _ -> unknown at name [ "Scalar"; "Interval"; "Series"; "Distribution"; "Categorical"; "Absent" ]

    let private readMethod (v: JsonValue) (at: string) : MethodRef =
        match case v at with
        | "Computed", x ->
            match
                arr x (at + ".Computed")
                |> List.mapi (fun i s -> str s (sprintf "%s.Computed[%d]" at i))
            with
            | [ operation; version; paramHash ] -> Computed(operation, version, paramHash)
            | _ ->
                refuse
                    RefusalClass.ShapeInvalid
                    (at + ".Computed")
                    "a computed method is an operation, a version and a parameter hash"
        | "HumanAsserted", x -> HumanAsserted(str x (at + ".HumanAsserted"))
        | "Imported", x -> Imported(str x (at + ".Imported"))
        | name, _ -> unknown at name [ "Computed"; "HumanAsserted"; "Imported" ]

    let private readDisclosure (v: JsonValue) (at: string) : Disclosure =
        match case v at with
        | "Surfaceable", JNull -> Surfaceable
        | "Internal", JNull -> Internal
        | "Restricted", x -> Restricted(str x (at + ".Restricted"))
        | name, _ -> unknown at name [ "Surfaceable"; "Internal"; "Restricted" ]

    let private readFact (raw: JsonValue) : Fact =
        let m = obj raw "$"
        let subject = obj (get m "Subject" "$") "$.Subject"
        let metric = obj (get m "Metric" "$") "$.Metric"
        let period = obj (get m "Period" "$") "$.Period"
        let evidence = obj (get m "Evidence" "$") "$.Evidence"

        {
            FactId = str (get m "FactId" "$") "$.FactId"
            Subject = {
                Hierarchy = str (get subject "Hierarchy" "$.Subject") "$.Subject.Hierarchy"
                Path =
                    arr (get subject "Path" "$.Subject") "$.Subject.Path"
                    |> List.mapi (fun i p -> str p (sprintf "$.Subject.Path[%d]" i))
            }
            Metric = MetricRef(str (get metric "MetricRef" "$.Metric") "$.Metric.MetricRef")
            Value = readValue (get m "Value" "$") "$.Value"
            Period = {
                From = instant (get period "From" "$.Period") "$.Period.From"
                To = instant (get period "To" "$.Period") "$.Period.To"
                Label = optStr (opt period "Label") "$.Period.Label"
            }
            AsOf = instant (get m "AsOf" "$") "$.AsOf"
            Method = readMethod (get m "Method" "$") "$.Method"
            Evidence = {
                ResultRef = optStr (opt evidence "ResultRef") "$.Evidence.ResultRef"
                InputHashes =
                    arr (get evidence "InputHashes" "$.Evidence") "$.Evidence.InputHashes"
                    |> List.mapi (fun i h -> str h (sprintf "$.Evidence.InputHashes[%d]" i))
                TriggerRef = optStr (opt evidence "TriggerRef") "$.Evidence.TriggerRef"
            }
            Confidence =
                match opt m "Confidence" with
                | JNull -> None
                | c ->
                    let cm = obj c "$.Confidence"

                    Some {
                        Value = quantity (get cm "Value" "$.Confidence") "$.Confidence.Value"
                        Basis = optStr (opt cm "Basis") "$.Confidence.Basis"
                    }
            Supersedes = optStr (opt m "Supersedes") "$.Supersedes"
            Disclosure = readDisclosure (get m "Disclosure" "$") "$.Disclosure"
        }

    /// Reads one fact in the legacy saved shape, holding it to every rule the specification puts on a
    /// reader of a fact (its id is verified, never trusted) except the token rules, or refuses it for
    /// one named class. The fact is returned as the package's own type, as stored; writing it writes the
    /// specification's shape, and is refused when it carries a token the specification refuses.
    let read (text: string) : Result<Fact, Refusal> =
        try
            match Json.parse text with
            | Error why -> Error(Refusal.make RefusalClass.ShapeInvalid "$" ("not JSON: " + why))
            | Ok raw -> readFact raw |> FactWire.checkStored
        with Refused r ->
            Error r
