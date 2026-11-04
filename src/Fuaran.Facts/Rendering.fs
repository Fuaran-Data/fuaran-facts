namespace Fuaran.Facts

/// ISO 4217 code → the symbol a figure in that currency is shown with. **Data, not culture**: the
/// table is the whole mapping and reads no culture data, so a rendering is the same on every host.
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

/// The glyphs a rendering is written with. The specification fixes the number — its rounding, its
/// digits, where a group boundary falls — and leaves the glyphs to the reader (FACT_WIRE.md §7.1).
type RenderGlyphs = {
    DecimalSeparator: string
    GroupSeparator: string
    PercentSign: string
    /// The prefix for a currency, from its ISO 4217 code.
    CurrencyPrefix: string -> string
}

[<RequireQualifiedAccess>]
module RenderGlyphs =

    /// `.`, `,`, `%` and `CurrencySymbols.prefix`: the invariant, English-reading glyphs.
    let invariant: RenderGlyphs = {
        DecimalSeparator = "."
        GroupSeparator = ","
        PercentSign = "%"
        CurrencyPrefix = CurrencySymbols.prefix
    }

/// A value shown as its metric declares (§7.1): the one rendering a value has, shared by every
/// surface that quotes it, so a value never renders two ways at two doors.
[<RequireQualifiedAccess>]
module FactRendering =

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

    let private group (glyphs: RenderGlyphs) (grouping: bool) (whole: string) : string =
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

            String.concat glyphs.GroupSeparator parts

    let private digitsText (glyphs: RenderGlyphs) (grouping: bool) (digits: int) (q: bigint) : string =
        let text = (string q).PadLeft(digits + 1, '0')
        let whole = text.Substring(0, text.Length - digits)
        let fraction = text.Substring(text.Length - digits)
        let whole = group glyphs grouping whole

        if digits = 0 then
            whole
        else
            whole + glyphs.DecimalSeparator + fraction

    /// One decimal shown under `format` for a metric in `unit`.
    let formatDecimal
        (glyphs: RenderGlyphs)
        (format: DisplayFormat)
        (unit: Fuaran.Core.UnitOfMeasure)
        (d: DecimalText)
        : string =
        match format.Style, format.FractionDigits with
        | Verbatim, _
        | _, None -> d.Text
        | Number, Some n ->
            let negative, q = rounded d 0 n
            (if negative then "-" else "") + digitsText glyphs format.Grouping n q
        | Percent, Some n ->
            let negative, q = rounded d 2 n

            (if negative then "-" else "")
            + digitsText glyphs format.Grouping n q
            + glyphs.PercentSign
        | Currency, Some n ->
            let negative, q = rounded d 0 n

            let prefix =
                match MetricUnit.currency unit with
                | Some code -> glyphs.CurrencyPrefix code
                | None -> ""

            (if negative then "-" else "") + prefix + digitsText glyphs format.Grouping n q

    let private renderValue (fmt: DecimalText -> string) (value: FactValue) : string =
        match value with
        | Scalar d -> fmt d
        | Interval(lo, hi) -> fmt lo + " to " + fmt hi
        // A series is named, never inlined: show the reference.
        | Series(address, _) -> "series " + address
        | Distribution buckets ->
            buckets
            |> Map.toList
            |> List.sortWith (fun (a, _) (b, _) -> Ordinal.compare a b)
            |> List.map (fun (k, d) -> k + ": " + fmt d)
            |> String.concat "; "
        | Categorical c -> c
        // An absence is shown honestly, never made into a number.
        | Absent reason -> "absent (" + reason + ")"

    /// `value` shown as `metric` declares it, with `glyphs`. With no metric, every quantity is shown
    /// verbatim.
    let renderWith (glyphs: RenderGlyphs) (metric: MetricDefinition option) (value: FactValue) : string =
        match metric with
        | None -> renderValue (fun d -> d.Text) value
        | Some m -> renderValue (formatDecimal glyphs m.DisplayFormat m.Unit) value

    /// `value` shown as `metric` declares it, with the invariant glyphs: `£21,240` for `21240.00` in a
    /// `[GBP]` metric formatted `currency`, 0 digits, grouped.
    let renderFor (metric: MetricDefinition option) (value: FactValue) : string =
        renderWith RenderGlyphs.invariant metric value
