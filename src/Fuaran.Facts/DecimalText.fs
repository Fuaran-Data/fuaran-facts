namespace Fuaran.Facts

/// A quantity as **decimal text** (FACT_WIRE.md §3.3): `-?(0|[1-9][0-9]*)(\.[0-9]+)?`, at most 28
/// digits after the point, a coefficient of at most 2^96 - 1, and no negative zero.
///
/// **The text is the value, and its scale is significant.** `412000.0` and `412000.00` are two
/// values: they are unequal here, and a person's assertion of each mints a different fact id. Nothing
/// in this library normalises decimal text. Numeric comparison, where a law needs one, is
/// `DecimalText.compare`, under which `1.5` and `1.50` are equal.
///
/// Held as text rather than as a platform decimal so the value is exactly the same on every host
/// this library compiles to; `DecimalText.ofDecimal` and `DecimalText.toDecimal` convert on .NET.
[<StructuralEquality; NoComparison>]
type DecimalText =
    private
    | DecimalText of string

    /// The decimal text, exactly as carried.
    member d.Text =
        let (DecimalText t) = d
        t

    override d.ToString() = d.Text

[<RequireQualifiedAccess>]
module DecimalText =

    let private maxCoefficient = 79228162514264337593543950335I

    /// 10^n for n >= 0, by repeated multiplication: the same answer on every host.
    let internal pow10 (n: int) : bigint =
        let mutable r = 1I

        for _ in 1..n do
            r <- r * 10I

        r

    /// The coefficient (the digits with the point removed, signed) and the scale (digits after the
    /// point) of text already known to be in the decimal grammar.
    let private split (text: string) : bigint * int =
        let negative = text.StartsWith "-"
        let body = if negative then text.Substring 1 else text
        let point = body.IndexOf '.'

        let whole, fraction =
            if point < 0 then
                body, ""
            else
                body.Substring(0, point), body.Substring(point + 1)

        let mutable c = 0I

        for ch in whole + fraction do
            c <- c * 10I + bigint (int ch - int '0')

        (if negative then -c else c), fraction.Length

    let private grammar (text: string) : bool =
        let n = text.Length
        let mutable i = if n > 0 && text[0] = '-' then 1 else 0
        let wholeStart = i

        while i < n && TextRules.isDigit text[i] do
            i <- i + 1

        let wholeLen = i - wholeStart

        let wholeOk = wholeLen > 0 && (wholeLen = 1 || text[wholeStart] <> '0')

        if not wholeOk then
            false
        elif i = n then
            true
        elif text[i] <> '.' then
            false
        else
            let fracStart = i + 1
            let mutable j = fracStart

            while j < n && TextRules.isDigit text[j] do
                j <- j + 1

            j = n && j > fracStart

    /// Reads `text` as decimal text, or refuses it (`decimal-invalid`) naming why. `at` names where
    /// the text came from.
    let tryParseAt (at: string) (text: string) : Result<DecimalText, Refusal> =
        let refuse why =
            Error(Refusal.make RefusalClass.DecimalInvalid at (sprintf "'%s' %s" text why))

        if isNull text then
            Error(Refusal.make RefusalClass.ShapeInvalid at "a decimal is a string")
        elif not (grammar text) then
            refuse "is not decimal text"
        else
            let coefficient, scale = split text

            if scale > 28 then
                refuse "has more than 28 digits after the point"
            elif abs coefficient > maxCoefficient then
                refuse "is beyond the 96-bit coefficient"
            elif text.StartsWith "-" && coefficient = 0I then
                refuse "is a negative zero"
            else
                Ok(DecimalText text)

    /// Reads `text` as decimal text, or refuses it.
    let tryParse (text: string) : Result<DecimalText, Refusal> = tryParseAt "decimal" text

    /// Reads decimal text, throwing `System.ArgumentException` when it is not. For literals.
    let parse (text: string) : DecimalText =
        match tryParse text with
        | Ok d -> d
        | Error r -> invalidArg "text" (Refusal.describe r)

    /// The signed coefficient and the scale: `-12.50` is `(-1250, 2)`.
    let coefficientAndScale (d: DecimalText) : bigint * int = split d.Text

    /// Numeric comparison: negative, zero or positive. `1.5` and `1.50` compare equal.
    let compare (a: DecimalText) (b: DecimalText) : int =
        let ca, sa = split a.Text
        let cb, sb = split b.Text
        let s = max sa sb
        let x = ca * pow10 (s - sa)
        let y = cb * pow10 (s - sb)

        if x < y then -1
        elif x > y then 1
        else 0

    /// Whether the value is below zero.
    let isNegative (d: DecimalText) : bool = d.Text.StartsWith "-"

#if !FABLE_COMPILER
    /// The decimal text of a .NET decimal, scale kept: `412000.00m` is `412000.00`. Every .NET
    /// decimal is in range.
    let ofDecimal (d: decimal) : DecimalText =
        let text = d.ToString(System.Globalization.CultureInfo.InvariantCulture)
        // A .NET decimal can hold a negative zero (`-0.00m`), which the wire refuses; it is zero.
        let text =
            if text.StartsWith "-" && d = 0m then
                text.Substring 1
            else
                text

        DecimalText text

    /// The .NET decimal of decimal text, scale kept. Every decimal text is exact in one.
    let toDecimal (d: DecimalText) : decimal =
        System.Decimal.Parse(d.Text, System.Globalization.CultureInfo.InvariantCulture)
#endif
