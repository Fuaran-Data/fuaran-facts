namespace Fuaran.Facts

// `Fuaran.Core.Hash` is reached through an abbreviation, never written in an expression: compiled from
// source beside other Fuaran.Core packages, a qualified path in an expression can resolve to a union case
// of the same name in that namespace (see Definitions.fs). An abbreviation resolves among modules only.
module CoreHash = Fuaran.Core.Hash

/// Ordinal order (FACT_WIRE.md §3.1 rule 4): by UTF-16 code unit, one unit at a time, a shorter
/// string first where one is a prefix of the other. Written out rather than delegated, so that the
/// order is the same on every host this library compiles to.
[<RequireQualifiedAccess>]
module Ordinal =

    /// Negative, zero or positive as `a` sorts before, with, or after `b`.
    let compare (a: string) (b: string) : int =
        let n = min a.Length b.Length
        let mutable i = 0
        let mutable result = 0

        while result = 0 && i < n do
            let d = int a[i] - int b[i]

            if d < 0 then
                result <- -1
            elif d > 0 then
                result <- 1

            i <- i + 1

        if result <> 0 then result
        elif a.Length = b.Length then 0
        elif a.Length < b.Length then -1
        else 1

    /// `xs` sorted ascending ordinally; duplicates kept.
    let sort (xs: string list) : string list = List.sortWith compare xs

/// Character-class helpers shared by the readers. Internal: the specification states each rule.
module internal TextRules =

    let isDigit (c: char) = c >= '0' && c <= '9'

    let isLowerHex (c: char) =
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')

    let allChars (p: char -> bool) (s: string) =
        let mutable ok = true
        let mutable i = 0

        while ok && i < s.Length do
            ok <- p s[i]
            i <- i + 1

        ok

    /// Whether every surrogate in `s` is half of a pair: the string is well-formed Unicode.
    let isWellFormed (s: string) : bool =
        let mutable ok = true
        let mutable i = 0

        while ok && i < s.Length do
            let c = int s[i]

            if c >= 0xD800 && c <= 0xDBFF then
                if i + 1 < s.Length && int s[i + 1] >= 0xDC00 && int s[i + 1] <= 0xDFFF then
                    i <- i + 2
                else
                    ok <- false
            elif c >= 0xDC00 && c <= 0xDFFF then
                ok <- false
            else
                i <- i + 1

        ok

    /// 64 lower-case hex digits: a fact id (§5.1).
    let isFactId (s: string) =
        not (isNull s) && s.Length = 64 && allChars isLowerHex s

    /// `sha256:` and 64 lower-case hex digits: a content address in `Fuaran.Core`'s form (§5.3).
    let isContentAddress (s: string) =
        not (isNull s)
        && s.Length = 71
        && s.StartsWith("sha256:")
        && allChars isLowerHex (s.Substring 7)

    /// A definition id (§7.1): `[a-z0-9][a-z0-9._-]*`.
    let isDefinitionId (s: string) =
        let lowerOrDigit (c: char) = (c >= 'a' && c <= 'z') || isDigit c

        not (isNull s)
        && s.Length > 0
        && lowerOrDigit s[0]
        && allChars (fun c -> lowerOrDigit c || c = '.' || c = '_' || c = '-') s

    /// Lower-case hex SHA-256 over the UTF-8 bytes of `text`: `Fuaran.Core`'s pure digest, the
    /// same value on .NET and under Fable.
    let sha256Hex (text: string) : string = CoreHash.sha256Hex text

    /// Lower-case hex SHA-256 of `bytes`.
    let sha256HexOfBytes (bytes: byte[]) : string = CoreHash.sha256HexOfBytes bytes
