namespace Fuaran.Facts

// `Fuaran.Core.Hash` is reached through an abbreviation, never written in an expression: compiled from
// source beside other Fuaran.Core packages, a qualified path in an expression can resolve to a union case
// of the same name in that namespace (see Definitions.fs). An abbreviation resolves among modules only.
module CoreHash = Fuaran.Core.Hash

/// A JSON value as read, members in document order, and as written. `JNumber` keeps the token's
/// text; `JMap` is written in ordinal key order (§3.1 rule 4) where `JObject` is written in the order
/// given (a record, rule 3).
type internal JsonValue =
    | JNull
    | JBool of bool
    | JNumber of string
    | JString of string
    | JArray of JsonValue list
    | JObject of (string * JsonValue) list
    | JMap of (string * JsonValue) list

/// The byte-level reader and the canonical writer. Pure F#: the same bytes on every host.
module internal Json =

    /// Strict UTF-8: no overlong form, no surrogate code point, nothing above U+10FFFF, no
    /// truncated sequence. `None` for bytes that are not UTF-8.
    let decodeUtf8 (bytes: byte[]) : string option =
        let sb = System.Text.StringBuilder()
        let n = bytes.Length
        let mutable i = 0
        let mutable ok = true

        let cont (k: int) = k < n && (int bytes[k] &&& 0xC0) = 0x80

        while ok && i < n do
            let b0 = int bytes[i]

            if b0 < 0x80 then
                sb.Append(char b0) |> ignore
                i <- i + 1
            elif b0 >= 0xC2 && b0 <= 0xDF && cont (i + 1) then
                sb.Append(char (((b0 &&& 0x1F) <<< 6) ||| (int bytes[i + 1] &&& 0x3F)))
                |> ignore

                i <- i + 2
            elif b0 >= 0xE0 && b0 <= 0xEF && cont (i + 1) && cont (i + 2) then
                let cp =
                    ((b0 &&& 0x0F) <<< 12)
                    ||| ((int bytes[i + 1] &&& 0x3F) <<< 6)
                    ||| (int bytes[i + 2] &&& 0x3F)

                if cp < 0x800 || (cp >= 0xD800 && cp <= 0xDFFF) then
                    ok <- false
                else
                    sb.Append(char cp) |> ignore
                    i <- i + 3
            elif b0 >= 0xF0 && b0 <= 0xF4 && cont (i + 1) && cont (i + 2) && cont (i + 3) then
                let cp =
                    ((b0 &&& 0x07) <<< 18)
                    ||| ((int bytes[i + 1] &&& 0x3F) <<< 12)
                    ||| ((int bytes[i + 2] &&& 0x3F) <<< 6)
                    ||| (int bytes[i + 3] &&& 0x3F)

                if cp < 0x10000 || cp > 0x10FFFF then
                    ok <- false
                else
                    let v = cp - 0x10000

                    sb.Append(char (0xD800 + (v >>> 10))).Append(char (0xDC00 + (v &&& 0x3FF)))
                    |> ignore

                    i <- i + 4
            else
                ok <- false

        if ok then Some(sb.ToString()) else None

    exception private JsonSyntax of string

    let private isWs (c: char) =
        c = ' ' || c = '\t' || c = '\n' || c = '\r'

    let private hexValue (c: char) =
        if c >= '0' && c <= '9' then int c - int '0'
        elif c >= 'a' && c <= 'f' then int c - int 'a' + 10
        elif c >= 'A' && c <= 'F' then int c - int 'A' + 10
        else -1

    /// Parses JSON text (RFC 8259; whitespace allowed). `Error` names where it stops being JSON.
    let parse (text: string) : Result<JsonValue, string> =
        let n = text.Length
        let mutable pos = 0

        let fail (msg: string) =
            raise (JsonSyntax(sprintf "%s at %d" msg pos))

        let skipWs () =
            while pos < n && isWs text[pos] do
                pos <- pos + 1

        let expect (c: char) =
            if pos < n && text[pos] = c then
                pos <- pos + 1
            else
                fail (sprintf "expected '%c'" c)

        let literal (word: string) (v: JsonValue) =
            if pos + word.Length <= n && text.Substring(pos, word.Length) = word then
                pos <- pos + word.Length
                v
            else
                fail "unexpected token"

        let parseString () : string =
            expect '"'
            let sb = System.Text.StringBuilder()
            let mutable fin = false

            while not fin do
                if pos >= n then
                    fail "unterminated string"

                let c = text[pos]

                if c = '"' then
                    pos <- pos + 1
                    fin <- true
                elif c = '\\' then
                    if pos + 1 >= n then
                        fail "unterminated escape"

                    let e = text[pos + 1]
                    pos <- pos + 2

                    match e with
                    | '"' -> sb.Append('"') |> ignore
                    | '\\' -> sb.Append('\\') |> ignore
                    | '/' -> sb.Append('/') |> ignore
                    | 'b' -> sb.Append('\b') |> ignore
                    | 'f' -> sb.Append('\012') |> ignore
                    | 'n' -> sb.Append('\n') |> ignore
                    | 'r' -> sb.Append('\r') |> ignore
                    | 't' -> sb.Append('\t') |> ignore
                    | 'u' ->
                        if pos + 4 > n then
                            fail "short \\u escape"

                        let mutable v = 0

                        for k in 0..3 do
                            let h = hexValue text[pos + k]

                            if h < 0 then
                                fail "bad \\u escape"

                            v <- v * 16 + h

                        sb.Append(char v) |> ignore
                        pos <- pos + 4
                    | _ -> fail "bad escape"
                elif int c < 0x20 then
                    fail "control character in a string"
                else
                    sb.Append(c) |> ignore
                    pos <- pos + 1

            sb.ToString()

        let parseNumber () : JsonValue =
            let start = pos

            let digits () =
                let s = pos

                while pos < n && TextRules.isDigit text[pos] do
                    pos <- pos + 1

                pos - s

            if pos < n && text[pos] = '-' then
                pos <- pos + 1

            if pos < n && text[pos] = '0' then
                pos <- pos + 1
            elif digits () = 0 then
                fail "bad number"

            if pos < n && text[pos] = '.' then
                pos <- pos + 1

                if digits () = 0 then
                    fail "bad fraction"

            if pos < n && (text[pos] = 'e' || text[pos] = 'E') then
                pos <- pos + 1

                if pos < n && (text[pos] = '+' || text[pos] = '-') then
                    pos <- pos + 1

                if digits () = 0 then
                    fail "bad exponent"

            JNumber(text.Substring(start, pos - start))

        let rec value () : JsonValue =
            skipWs ()

            if pos >= n then
                fail "unexpected end"

            match text[pos] with
            | '{' ->
                pos <- pos + 1
                skipWs ()
                let members = ResizeArray<string * JsonValue>()

                if pos < n && text[pos] = '}' then
                    pos <- pos + 1
                else
                    let mutable fin = false

                    while not fin do
                        skipWs ()
                        let k = parseString ()
                        skipWs ()
                        expect ':'
                        let v = value ()
                        members.Add((k, v))
                        skipWs ()

                        if pos < n && text[pos] = ',' then
                            pos <- pos + 1
                        else
                            expect '}'
                            fin <- true

                JObject(List.ofSeq members)
            | '[' ->
                pos <- pos + 1
                skipWs ()
                let items = ResizeArray<JsonValue>()

                if pos < n && text[pos] = ']' then
                    pos <- pos + 1
                else
                    let mutable fin = false

                    while not fin do
                        items.Add(value ())
                        skipWs ()

                        if pos < n && text[pos] = ',' then
                            pos <- pos + 1
                        else
                            expect ']'
                            fin <- true

                JArray(List.ofSeq items)
            | '"' -> JString(parseString ())
            | 't' -> literal "true" (JBool true)
            | 'f' -> literal "false" (JBool false)
            | 'n' -> literal "null" JNull
            | c when c = '-' || TextRules.isDigit c -> parseNumber ()
            | _ -> fail "unexpected character"

        try
            let v = value ()
            skipWs ()

            if pos <> n then
                fail "trailing content"

            Ok v
        with JsonSyntax msg ->
            Error msg

    let private writeString (sb: System.Text.StringBuilder) (s: string) =
        sb.Append('"') |> ignore

        for c in s do
            match c with
            | '"' -> sb.Append("\\\"") |> ignore
            | '\\' -> sb.Append("\\\\") |> ignore
            | '\b' -> sb.Append("\\b") |> ignore
            | '\t' -> sb.Append("\\t") |> ignore
            | '\n' -> sb.Append("\\n") |> ignore
            | '\012' -> sb.Append("\\f") |> ignore
            | '\r' -> sb.Append("\\r") |> ignore
            | c when int c < 0x20 ->
                let hex = "0123456789abcdef"
                sb.Append("\\u00").Append(hex[int c >>> 4]).Append(hex[int c &&& 0xF]) |> ignore
            | c -> sb.Append(c) |> ignore

        sb.Append('"') |> ignore

    /// The canonical encoding (§3.1): no whitespace, records in the order given, maps in ordinal key
    /// order, strings escaped with lower-case hex.
    let write (v: JsonValue) : string =
        let sb = System.Text.StringBuilder()

        let rec go (v: JsonValue) =
            match v with
            | JNull -> sb.Append("null") |> ignore
            | JBool true -> sb.Append("true") |> ignore
            | JBool false -> sb.Append("false") |> ignore
            | JNumber t -> sb.Append(t) |> ignore
            | JString s -> writeString sb s
            | JArray items ->
                sb.Append('[') |> ignore

                items
                |> List.iteri (fun i x ->
                    if i > 0 then
                        sb.Append(',') |> ignore

                    go x)

                sb.Append(']') |> ignore
            | JObject members -> pairs members
            | JMap members -> pairs (members |> List.sortWith (fun (a, _) (b, _) -> Ordinal.compare a b))

        and pairs (members: (string * JsonValue) list) =
            sb.Append('{') |> ignore

            members
            |> List.iteri (fun i (k, x) ->
                if i > 0 then
                    sb.Append(',') |> ignore

                writeString sb k
                sb.Append(':') |> ignore
                go x)

            sb.Append('}') |> ignore

        go v
        sb.ToString()

    /// The UTF-8 bytes of `text` (`Fuaran.Core`'s encoder).
    let utf8Bytes (text: string) : byte[] = CoreHash.utf8Bytes text
