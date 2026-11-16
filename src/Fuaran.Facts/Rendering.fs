namespace Fuaran.Facts

/// ISO 4217 code → the symbol a figure in that currency is shown with (FACT_WIRE.md §10.4). **Data,
/// not culture**: the table is the whole mapping and reads no culture data, so a rendering is the same
/// on every host.
/// The symbols are the English (CLDR `en`) prefix forms, so a figure reads unambiguously in English
/// prose: the US dollar alone takes the bare `$`, every other dollar names its country (`A$`,
/// `CA$`). A code the table does not hold is shown with the code itself as its prefix (`CHF 1,299`).
[<RequireQualifiedAccess>]
module CurrencySymbols =

    /// The known codes and their symbols — the whole table.
    let table: Map<string, string> =
        Map.ofList [
            "AUD", "A$"
            "BRL", "R$"
            "CAD", "CA$"
            "CNY", "CN¥"
            "EUR", "€"
            "GBP", "£"
            "HKD", "HK$"
            "ILS", "₪"
            "INR", "₹"
            "JPY", "¥"
            "KRW", "₩"
            "MXN", "MX$"
            "NZD", "NZ$"
            "PHP", "₱"
            "TWD", "NT$"
            "USD", "$"
            "VND", "₫"
        ]

    /// The prefix a figure in currency `code` (three capitals) is shown with: the table's symbol, or
    /// the code and a space.
    let prefix (code: string) : string =
        match table |> Map.tryFind code with
        | Some symbol -> symbol
        | None -> code + " "

/// The rendering standard (FACT_WIRE.md §10): the one text a value is shown as under its metric's
/// display format, the same on every host, glyphs included. A host may show a person a localised form,
/// but that form is not the rendering and is never what a quotation is checked against.
[<RequireQualifiedAccess>]
module FactRendering =

    /// The rendering rule this module implements (§10.1). A record that keeps a rendering for later
    /// checking records this number; a later rule is a new number, and rule 1 is never redefined.
    [<Literal>]
    let Rule = 1

    /// `d` rounded half away from zero to `digits` places: (negative, digits of the magnitude with
    /// the point removed). Exact, on bigint, on every host.
    let private rounded (d: DecimalText) (shiftLeft: int) (digits: int) : bool * bigint =
        let c, s = DecimalText.coefficientAndScale d
        let negative = c < 0I
        let magnitude = abs c
        // The value is magnitude * 10^(shiftLeft - s); its digits at `digits` places are
        // magnitude * 10^(digits + shiftLeft - s), rounded half away from zero.
        let e = digits + shiftLeft - s

        let q =
            if e >= 0 then
                magnitude * DecimalText.pow10 e
            else
                let divisor = DecimalText.pow10 (-e)
                let q = magnitude / divisor
                let r = magnitude % divisor
                if r * 2I >= divisor then q + 1I else q

        (negative && q <> 0I), q

    let private group (grouping: bool) (whole: string) : string =
        if not grouping || whole.Length <= 3 then
            whole
        else
            let first = whole.Length % 3
            let parts = ResizeArray<string>()

            if first > 0 then
                parts.Add(whole.Substring(0, first))

            let mutable i = first

            while i < whole.Length do
                parts.Add(whole.Substring(i, 3))
                i <- i + 3

            String.concat "," parts

    /// §10.2: the digits of `q` at `digits` places, `.` before the fraction, `,` between groups.
    let private digitsText (grouping: bool) (digits: int) (q: bigint) : string =
        let text = (string q).PadLeft(digits + 1, '0')
        let whole = text.Substring(0, text.Length - digits)
        let fraction = text.Substring(text.Length - digits)
        let whole = group grouping whole

        if digits = 0 then whole else whole + "." + fraction

    /// One decimal shown under `format` for a metric in `unit` (§10.3): `-` before everything when the
    /// rounded value is below zero, `%` with no space, a currency's prefix before the digits and a
    /// prefixed unit's symbol after them (`£12.50k` in `k[GBP]`).
    let formatDecimal (format: DisplayFormat) (unit: Fuaran.Core.UnitOfMeasure) (d: DecimalText) : string =
        match format.Style, format.FractionDigits with
        | Verbatim, _
        | _, None -> d.Text
        | Number, Some n ->
            let negative, q = rounded d 0 n
            (if negative then "-" else "") + digitsText format.Grouping n q
        | Percent, Some n ->
            let negative, q = rounded d 2 n
            (if negative then "-" else "") + digitsText format.Grouping n q + "%"
        | Currency, Some n ->
            let negative, q = rounded d 0 n

            let prefix, unitPrefix =
                match MetricUnit.currencyParts unit with
                | Some(code, unitPrefix) -> CurrencySymbols.prefix code, unitPrefix
                | None -> "", ""

            (if negative then "-" else "")
            + prefix
            + digitsText format.Grouping n q
            + unitPrefix

    let private renderValue (fmt: DecimalText -> string) (value: FactValue) : string =
        match value with
        | Scalar d -> fmt d
        | Interval(lo, hi) -> fmt lo + " to " + fmt hi
        // A series is named, never inlined: show the reference.
        | Series(address, _) -> "series " + address.Text
        | Distribution buckets ->
            buckets
            |> Map.toList
            |> List.sortWith (fun (a, _) (b, _) -> Ordinal.compare a b)
            |> List.map (fun (k, d) -> k + ": " + fmt d)
            |> String.concat "; "
        | Categorical c -> c
        // An absence is shown honestly, never made into a number.
        | Absent reason -> "absent (" + reason + ")"

    /// `value` shown as `metric` declares it, under rule 1 (§10.5): `£21,240` for `21240.00` in a
    /// `[GBP]` metric formatted `currency`, 0 digits, grouped. With no metric, every quantity is shown
    /// verbatim.
    let renderFor (metric: MetricDefinition option) (value: FactValue) : string =
        match metric with
        | None -> renderValue (fun d -> d.Text) value
        | Some m -> renderValue (formatDecimal m.DisplayFormat m.Unit) value

    /// `value` shown under the rendering rule `rule` names, or a refusal (`version-unsupported`) for a
    /// rule this version does not implement. A quotation recorded under a rule is checked here, under
    /// that rule.
    let renderUnder (rule: int) (metric: MetricDefinition option) (value: FactValue) : Result<string, Refusal> =
        if rule = Rule then
            Ok(renderFor metric value)
        else
            Error(
                Refusal.make
                    RefusalClass.VersionUnsupported
                    "rule"
                    (sprintf "rendering rule %d; this version implements rule %d" rule Rule)
            )
