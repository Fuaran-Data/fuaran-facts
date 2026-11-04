namespace Fuaran.Facts

/// A UTC instant on the proleptic Gregorian calendar, at a resolution of 100 nanoseconds — a
/// **tick** (FACT_WIRE.md §3.4). Held as ticks since 0001-01-01T00:00:00Z, which is the count a
/// .NET `DateTime` holds, so the two convert without loss. Instants compare as points in time.
[<StructuralEquality; StructuralComparison>]
type Instant =
    private
    | Instant of int64

    /// Ticks (100 ns) since 0001-01-01T00:00:00Z.
    member i.Ticks =
        let (Instant t) = i
        t

[<RequireQualifiedAccess>]
module Instant =

    [<Literal>]
    let TicksPerSecond = 10000000L

    let private ticksPerDay = 86400L * TicksPerSecond

    let private isLeap (y: int) =
        (y % 4 = 0 && y % 100 <> 0) || y % 400 = 0

    let private monthDays = [| 31; 28; 31; 30; 31; 30; 31; 31; 30; 31; 30; 31 |]

    let private daysIn (y: int) (m: int) =
        monthDays[m - 1] + (if m = 2 && isLeap y then 1 else 0)

    /// Days from 0001-01-01 to y-m-d.
    let private dayNumber (y: int) (m: int) (d: int) : int64 =
        let py = int64 (y - 1)
        let mutable days = py * 365L + py / 4L - py / 100L + py / 400L

        for i in 1 .. m - 1 do
            days <- days + int64 (daysIn y i)

        days + int64 (d - 1)

    /// The latest instant the wire can write: 9999-12-31T23:59:59.9999999Z.
    let maxTicks = dayNumber 10000 1 1 * ticksPerDay - 1L

    /// The instant `ticks` after 0001-01-01T00:00:00Z, or `None` outside 0001–9999.
    let tryOfTicks (ticks: int64) : Instant option =
        if ticks < 0L || ticks > maxTicks then
            None
        else
            Some(Instant ticks)

    /// The instant `ticks` after 0001-01-01T00:00:00Z; throws outside 0001–9999.
    let ofTicks (ticks: int64) : Instant =
        match tryOfTicks ticks with
        | Some i -> i
        | None -> invalidArg "ticks" (sprintf "%d ticks is outside 0001-9999" ticks)

    let private digitsAt (s: string) (start: int) (count: int) : int option =
        if start + count > s.Length then
            None
        else
            let mutable v = 0
            let mutable ok = true

            for i in start .. start + count - 1 do
                if TextRules.isDigit s[i] then
                    v <- v * 10 + (int s[i] - int '0')
                else
                    ok <- false

            if ok then Some v else None

    /// Reads an instant's wire text — `YYYY-MM-DDTHH:MM:SS[.F]Z`, the fraction omitted when whole
    /// and otherwise one to seven digits with no trailing zero — or refuses it (`instant-invalid`).
    let tryParseAt (at: string) (text: string) : Result<Instant, Refusal> =
        let refuse why =
            Error(Refusal.make RefusalClass.InstantInvalid at (sprintf "'%s' %s" text why))

        if isNull text then
            Error(Refusal.make RefusalClass.ShapeInvalid at "an instant is a string")
        else
            let fixedOk =
                text.Length >= 20
                && text[4] = '-'
                && text[7] = '-'
                && text[10] = 'T'
                && text[13] = ':'
                && text[16] = ':'
                && text[text.Length - 1] = 'Z'

            let parts =
                if not fixedOk then
                    None
                else
                    match
                        digitsAt text 0 4,
                        digitsAt text 5 2,
                        digitsAt text 8 2,
                        digitsAt text 11 2,
                        digitsAt text 14 2,
                        digitsAt text 17 2
                    with
                    | Some y, Some mo, Some d, Some h, Some mi, Some s -> Some(y, mo, d, h, mi, s)
                    | _ -> None

            let fraction =
                if not fixedOk then
                    None
                elif text.Length = 20 then
                    Some ""
                elif text[19] = '.' && text.Length > 21 then
                    let f = text.Substring(20, text.Length - 21)

                    if TextRules.allChars TextRules.isDigit f then
                        Some f
                    else
                        None
                else
                    None

            match parts, fraction with
            | Some(y, mo, d, h, mi, s), Some f ->
                if y < 1 then
                    refuse "is before year 0001"
                elif mo < 1 || mo > 12 then
                    refuse "names no month"
                elif d < 1 || d > daysIn y mo then
                    refuse "names no day of its month"
                elif h > 23 || mi > 59 || s > 59 then
                    refuse "has a time of day out of range"
                elif f.Length > 7 then
                    refuse "is finer than 100 ns"
                elif f.Length > 0 && f.EndsWith "0" then
                    refuse "carries a trailing zero in its fraction"
                else
                    let sub = if f = "" then 0L else int64 (f.PadRight(7, '0'))

                    let ticks =
                        (dayNumber y mo d * 86400L + int64 (h * 3600 + mi * 60 + s)) * TicksPerSecond
                        + sub

                    Ok(Instant ticks)
            | _ -> refuse "is not a UTC instant"

    /// Reads an instant's wire text, or refuses it.
    let tryParse (text: string) : Result<Instant, Refusal> = tryParseAt "instant" text

    /// Reads an instant's wire text, throwing `System.ArgumentException` when it is not one.
    let parse (text: string) : Instant =
        match tryParse text with
        | Ok i -> i
        | Error r -> invalidArg "text" (Refusal.describe r)

    /// (year, month, day, hour, minute, second, sub-second ticks).
    let private civil (Instant ticks) =
        let mutable days = ticks / ticksPerDay
        let rem = ticks % ticksPerDay
        let mutable y = 1
        // Whole 400-year cycles first, then year by year: bounded, exact and host-independent.
        let cycle = 146097L
        y <- y + int (days / cycle) * 400
        days <- days % cycle

        while days >= (if isLeap y then 366L else 365L) do
            days <- days - (if isLeap y then 366L else 365L)
            y <- y + 1

        let mutable m = 1

        while days >= int64 (daysIn y m) do
            days <- days - int64 (daysIn y m)
            m <- m + 1

        let secs = rem / TicksPerSecond
        y, m, int days + 1, int (secs / 3600L), int (secs / 60L % 60L), int (secs % 60L), rem % TicksPerSecond

    let private pad (width: int) (v: int64) = (string v).PadLeft(width, '0')

    let private stem (y, m, d, h, mi, s) =
        pad 4 (int64 y)
        + "-"
        + pad 2 (int64 m)
        + "-"
        + pad 2 (int64 d)
        + "T"
        + pad 2 (int64 h)
        + ":"
        + pad 2 (int64 mi)
        + ":"
        + pad 2 (int64 s)

    /// The wire text (§3.4): the fraction omitted when whole, else with no trailing zero.
    let toWire (i: Instant) : string =
        let y, m, d, h, mi, s, sub = civil i
        let head = stem (y, m, d, h, mi, s)

        if sub = 0L then
            head + "Z"
        else
            head + "." + (pad 7 sub).TrimEnd('0') + "Z"

    /// The identity rendering (§4.3): always seven fractional digits, zero-padded.
    let toIdentity (i: Instant) : string =
        let y, m, d, h, mi, s, sub = civil i
        stem (y, m, d, h, mi, s) + "." + pad 7 sub + "Z"

    /// `i` moved by `seconds` whole seconds, or `None` past either end of the calendar.
    let tryAddSeconds (seconds: int64) (i: Instant) : Instant option =
        tryOfTicks (i.Ticks + seconds * TicksPerSecond)

#if !FABLE_COMPILER
    /// The instant a .NET `DateTime` names. A `Utc` value is taken as it is, an `Unspecified` value
    /// is read AS UTC, and a `Local` value is converted — so the instant is a function of the value
    /// alone, never of the zone of the machine reading it.
    let ofDateTime (d: System.DateTime) : Instant =
        let utc =
            match d.Kind with
            | System.DateTimeKind.Utc -> d
            | System.DateTimeKind.Unspecified -> System.DateTime.SpecifyKind(d, System.DateTimeKind.Utc)
            | _ -> d.ToUniversalTime()

        Instant utc.Ticks

    /// The instant as a `Utc` .NET `DateTime`.
    let toDateTime (i: Instant) : System.DateTime =
        System.DateTime(i.Ticks, System.DateTimeKind.Utc)
#endif
