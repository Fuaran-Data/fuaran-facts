namespace Fuaran.Facts

// Fuaran.Core is never opened here: compiled from source under Fable, its internal names are in
// scope and one of them (`Currency`) would shadow this library's own.

/// A metric's unit (FACT_WIRE.md §7.2): a unit in `Fuaran.Core.Unit`'s algebra, written as its
/// canonical text. The currency is the unit's: a metric is in a currency exactly when its unit is
/// that currency to the first power, optionally prefixed (`[GBP]`, `k[GBP]`).
[<RequireQualifiedAccess>]
module MetricUnit =

    /// The canonical text of a unit (`Fuaran.Core.Unit.render`).
    let text (u: Fuaran.Core.UnitOfMeasure) : string = Fuaran.Core.Unit.render u

    /// Reads unit text, refusing (`unit-invalid`) text that does not parse, names something not
    /// admitted, or is not the canonical text of the unit it names.
    let tryParseAt (at: string) (text': string) : Result<Fuaran.Core.UnitOfMeasure, Refusal> =
        match Fuaran.Core.Unit.parse text' with
        | Error why -> Error(Refusal.make RefusalClass.UnitInvalid at (sprintf "'%s' is not a unit: %A" text' why))
        | Ok u ->
            let canonical = Fuaran.Core.Unit.render u

            if canonical = text' then
                Ok u
            else
                Error(
                    Refusal.make
                        RefusalClass.UnitInvalid
                        at
                        (sprintf "'%s' is not canonical unit text; the unit is written '%s'" text' canonical)
                )

    /// Reads canonical unit text.
    let tryParse (text': string) : Result<Fuaran.Core.UnitOfMeasure, Refusal> = tryParseAt "unit" text'

    /// The currency a unit is — one currency factor to the first power, optionally prefixed — as its
    /// ISO 4217 code and the prefix's symbol (`k[GBP]` is `("GBP", "k")`, `[GBP]` is `("GBP", "")`), or
    /// `None` (§7.2).
    let currencyParts (u: Fuaran.Core.UnitOfMeasure) : (string * string) option =
        let t = Fuaran.Core.Unit.render u
        let n = t.Length

        let isCap (c: char) = c >= 'A' && c <= 'Z'

        if
            n >= 5
            && t[n - 1] = ']'
            && t[n - 5] = '['
            && isCap t[n - 4]
            && isCap t[n - 3]
            && isCap t[n - 2]
            && TextRules.allChars (fun c -> (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) (t.Substring(0, n - 5))
        then
            Some(t.Substring(n - 4, 3), t.Substring(0, n - 5))
        else
            None

    /// The ISO 4217 code of the currency a unit is, or `None` (§7.2).
    let currency (u: Fuaran.Core.UnitOfMeasure) : string option = currencyParts u |> Option.map fst

/// Matching for a metric's canonical-method selector (§7.1): a full method identity, or a prefix of
/// one ending at a `:` boundary — `computed:rollup` selects every version and parameterisation of
/// `rollup`, and never `computed:rollup-alt:1:p0`. Ordinal throughout.
[<RequireQualifiedAccess>]
module CanonicalMethod =

    /// Whether `methodIdentity` (`Fact.methodIdentity`) matches `selector`.
    let matches (selector: string) (methodIdentity: string) : bool =
        methodIdentity = selector || methodIdentity.StartsWith(selector + ":")

/// Rules on a metric's display format (§7.1).
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DisplayFormat =

    /// The verbatim format: the decimal text as carried.
    let verbatim: DisplayFormat = {
        Style = Verbatim
        FractionDigits = None
        Grouping = false
    }

    /// Checks `f` against §7.1 for a metric in `unit`: `Verbatim` carries no digits and no
    /// grouping, every other style states 0–28 fraction digits, and `Currency` needs a unit that is
    /// one currency. Refuses `display-format-invalid` (or `shape-invalid` for digits out of range).
    let checkAt (at: string) (unit: Fuaran.Core.UnitOfMeasure) (f: DisplayFormat) : Result<unit, Refusal> =
        let refuse c msg = Error(Refusal.make c at msg)

        match f.FractionDigits with
        | Some d when d < 0 || d > 28 ->
            refuse RefusalClass.ShapeInvalid (sprintf "%d fraction digits is outside 0..28" d)
        | _ ->
            match f.Style, f.FractionDigits with
            | Verbatim, None when not f.Grouping -> Ok()
            | Verbatim, _ ->
                refuse RefusalClass.DisplayFormatInvalid "'verbatim' carries no fraction digits and no grouping"
            | _, None ->
                refuse RefusalClass.DisplayFormatInvalid "every style but 'verbatim' states its fraction digits"
            | Currency, Some _ when (MetricUnit.currency unit).IsNone ->
                refuse
                    RefusalClass.DisplayFormatInvalid
                    (sprintf
                        "'currency' needs a unit that is one currency, and '%s' is not"
                        (Fuaran.Core.Unit.render unit))
            | _, Some _ -> Ok()

/// Checks on definitions (§7.1, §7.3), the rules a reader applies, for a definition built in code.
[<RequireQualifiedAccess>]
module Definition =

    let private refuse c at msg = Error(Refusal.make c at msg)

    let private nonEmpty (at: string) (s: string) =
        if isNull s || s.Length = 0 then
            refuse RefusalClass.ShapeInvalid at "must not be empty"
        elif not (TextRules.isWellFormed s) then
            refuse RefusalClass.ShapeInvalid at "is not well-formed Unicode"
        else
            Ok()

    let private optNonEmpty (at: string) (s: string option) =
        match s with
        | None -> Ok()
        | Some v -> nonEmpty at v

    let private definitionId (at: string) (id: string) =
        match nonEmpty at id with
        | Error e -> Error e
        | Ok() ->
            if TextRules.isDefinitionId id then
                Ok()
            else
                refuse RefusalClass.TokenInvalid at (sprintf "'%s' is not a definition id" id)

    let private all (checks: (unit -> Result<unit, Refusal>) list) : Result<unit, Refusal> =
        checks |> List.fold (fun acc next -> acc |> Result.bind next) (Ok())

    /// Checks a metric definition, or refuses it with the class a reader would.
    let checkMetric (d: MetricDefinition) : Result<unit, Refusal> =
        all [
            fun () -> definitionId "metricDefinition.id" d.Id
            fun () -> nonEmpty "metricDefinition.name" d.Name
            fun () -> nonEmpty "metricDefinition.dimensionality" d.Dimensionality
            fun () -> optNonEmpty "metricDefinition.producingOperation" d.ProducingOperation
            fun () -> optNonEmpty "metricDefinition.canonicalMethod" d.CanonicalMethod
            fun () -> optNonEmpty "metricDefinition.context" d.Context
            fun () -> DisplayFormat.checkAt "metricDefinition.displayFormat" d.Unit d.DisplayFormat
            fun () ->
                match d.Staleness with
                | FreshFor w when w < 1 ->
                    refuse
                        RefusalClass.ShapeInvalid
                        "metricDefinition.staleness.windowSeconds"
                        (sprintf "%d is outside 1..2147483647" w)
                | _ -> Ok()
            fun () ->
                match d.RollUp with
                | Some(Additive t) when DecimalText.isNegative t ->
                    refuse RefusalClass.DecimalInvalid "metricDefinition.rollUp.tolerance" "a tolerance is not negative"
                | _ -> Ok()
        ]

    /// Checks a subject definition, or refuses it with the class a reader would.
    let checkSubject (d: SubjectDefinition) : Result<unit, Refusal> =
        all [
            fun () ->
                if List.isEmpty d.Levels then
                    refuse RefusalClass.ShapeInvalid "subjectDefinition.levels" "a hierarchy has at least one level"
                else
                    d.Levels
                    |> List.mapi (fun i l -> fun () -> nonEmpty (sprintf "subjectDefinition.levels[%d]" i) l)
                    |> all
            fun () -> definitionId "subjectDefinition.id" d.Id
            fun () -> nonEmpty "subjectDefinition.name" d.Name
            fun () -> optNonEmpty "subjectDefinition.calendar" d.Calendar
        ]

/// Conversions from the free-text forms an earlier generation of the kernel stored — a unit as a
/// free string (`GBP`, `count`, `%`) and a display format as a .NET numeric format string (`N0`,
/// `C2`, `P1`) — to the specification's typed forms. Each conversion is a table, stated in full in
/// `DECISIONS.md`, and **refuses rather than guesses**: a value the table does not hold, and that is
/// not already the thing it is converted to, is a refusal naming it.
[<RequireQualifiedAccess>]
module Legacy =

    /// The free-text unit words that name a dimensionless count or ratio, and so convert to `1`.
    /// `bytes` is a count: `Fuaran.Core.Unit` admits no unit of information (FACT_WIRE.md §7.2).
    let dimensionlessWords: string list = [ "1"; "count"; "items"; "units"; "ratio"; "bytes" ]

    let private isIsoCode (s: string) =
        s.Length = 3 && TextRules.allChars (fun c -> c >= 'A' && c <= 'Z') s

    /// The unit a free-text unit names: a dimensionless word is `1`; a bare ISO 4217 code (three
    /// capitals) is that currency (`GBP` is `[GBP]`); anything else must already be unit text
    /// `Fuaran.Core.Unit` parses (`%`, `ms`, `[GBP]`), and is refused (`unit-invalid`) when it is
    /// not — an empty string, a lower-case code, a word the table does not hold.
    let unitOf (legacy: string) : Result<Fuaran.Core.UnitOfMeasure, Refusal> =
        let at = "legacy.unit"

        if isNull legacy || legacy.Length = 0 then
            Error(Refusal.make RefusalClass.UnitInvalid at "an empty unit names nothing")
        elif List.contains legacy dimensionlessWords then
            Ok Fuaran.Core.Unit.dimensionless
        elif isIsoCode legacy then
            MetricUnit.tryParseAt at ("[" + legacy + "]")
        else
            match Fuaran.Core.Unit.parse legacy with
            | Ok u -> Ok u
            | Error why ->
                Error(
                    Refusal.make
                        RefusalClass.UnitInvalid
                        at
                        (sprintf "'%s' is neither a word the conversion table holds nor a unit: %A" legacy why)
                )

    /// A custom numeric pattern of one of the four plain shapes — `0`, `0.` and zeros, `#,##0`,
    /// `#,##0.` and zeros — each optionally followed by `%`: its style, fraction digits and grouping.
    /// Each names exactly one display format, so it converts; any other pattern does not.
    let private plainPattern (pattern: string) : (DisplayStyle * int * bool) option =
        let body, style =
            if pattern.EndsWith "%" then
                pattern.Substring(0, pattern.Length - 1), Percent
            else
                pattern, Number

        let grouping, rest =
            if body.StartsWith "#,##0" then true, body.Substring 5
            elif body.StartsWith "0" then false, body.Substring 1
            else false, "?"

        if rest = "" then
            Some(style, 0, grouping)
        elif
            rest.Length >= 2
            && rest[0] = '.'
            && TextRules.allChars (fun c -> c = '0') (rest.Substring 1)
        then
            Some(style, rest.Length - 1, grouping)
        else
            None

    /// The standard format a refused custom pattern should be written as, for the refusal to name.
    let private replacementFor (pattern: string) : string =
        let digits =
            match pattern.IndexOf '.' with
            | -1 -> 0
            | i ->
                pattern.Substring(i + 1)
                |> Seq.takeWhile (fun c -> c = '0' || c = '#')
                |> Seq.length

        if pattern |> Seq.exists (fun c -> not ("#0,.%".Contains c)) then
            sprintf
                "write the currency as the metric's unit and use 'C%d', or use 'N%d' for a plain number"
                digits
                digits
        elif pattern.Contains "%" then
            sprintf "use 'P%d'" digits
        elif pattern.Contains "," then
            sprintf "use 'N%d'" digits
        else
            sprintf "use 'F%d'" digits

    /// The display format a stored .NET numeric format string names (`DECISIONS.md` D5, D13):
    ///
    ///   - `""` is verbatim;
    ///   - a standard format: `N<d>` a grouped number, `F<d>` an ungrouped one, `P<d>` a grouped
    ///     percent and `C<d>` a grouped currency, each with `d` fraction digits (0–28; 2 when omitted,
    ///     the invariant culture's default), either letter case;
    ///   - a custom pattern of one of four plain shapes, which each name one format: `0` and `0.00` an
    ///     ungrouped number, `#,##0` and `#,##0.00` a grouped one, each with as many digits as zeros
    ///     after the point, and the same followed by `%` a percent.
    ///
    /// Every other string is refused (`display-format-invalid`), and the refusal names the standard
    /// format to write instead: a pattern with literal text (`£#,##0`) restates a currency the unit
    /// already makes, and any other pattern's meaning is a formatting library's, not data.
    let displayFormatOf (legacy: string) : Result<DisplayFormat, Refusal> =
        let at = "legacy.displayFormat"

        let refuse msg =
            Error(Refusal.make RefusalClass.DisplayFormatInvalid at msg)

        if isNull legacy || legacy.Length = 0 then
            Ok DisplayFormat.verbatim
        else
            let standard () =
                let digits = legacy.Substring 1

                let style =
                    match legacy[0] with
                    | 'N'
                    | 'n' -> Some(Number, true)
                    | 'F'
                    | 'f' -> Some(Number, false)
                    | 'P'
                    | 'p' -> Some(Percent, true)
                    | 'C'
                    | 'c' -> Some(Currency, true)
                    | _ -> None

                match style with
                | Some(s, grouping) when TextRules.allChars TextRules.isDigit digits && digits.Length <= 2 ->
                    Some(s, (if digits = "" then 2 else int digits), grouping)
                | _ -> None

            match plainPattern legacy |> Option.orElseWith standard with
            | None ->
                refuse (
                    sprintf "'%s' is a custom pattern the conversion does not hold; %s" legacy (replacementFor legacy)
                )
            | Some(_, d, _) when d > 28 -> refuse (sprintf "'%s' asks for more than 28 fraction digits" legacy)
            | Some(style, d, grouping) ->
                Ok {
                    Style = style
                    FractionDigits = Some d
                    Grouping = grouping
                }

#if !FABLE_COMPILER
    /// The staleness window a `System.TimeSpan` names, in whole seconds (1 to 2147483647), or a
    /// refusal (`shape-invalid`) for a window that is not a whole number of seconds or is out of
    /// range.
    let windowSecondsOf (window: System.TimeSpan) : Result<int, Refusal> =
        let at = "legacy.staleness"

        if window.Ticks % Instant.TicksPerSecond <> 0L then
            Error(Refusal.make RefusalClass.ShapeInvalid at (sprintf "%O is not a whole number of seconds" window))
        else
            let s = window.Ticks / Instant.TicksPerSecond

            if s < 1L || s > 2147483647L then
                Error(Refusal.make RefusalClass.ShapeInvalid at (sprintf "%d seconds is outside 1..2147483647" s))
            else
                Ok(int s)
#endif
