namespace Fuaran.Facts

/// The closed vocabulary a reader refuses a document for (FACT_WIRE.md §3.6). A refusal names
/// exactly one class; a conformance vector that names a class is passed only by refusing for that
/// class.
[<RequireQualifiedAccess>]
type RefusalClass =
    | NotCanonical
    | ShapeInvalid
    | KindUnknown
    | VersionUnsupported
    | DecimalInvalid
    | InstantInvalid
    | TokenInvalid
    | IntervalInverted
    | PeriodInverted
    | FactIdMismatch
    | UnitInvalid
    | DisplayFormatInvalid
    | RegistryUnordered
    | HistoryIdRepeated
    | SupersedesUnknown
    | SupersedesCrossLineage
    | SupersedesNotLater
    | SupersedesNotHead
    | SupersedesMissing
    | ViewMismatch
    | StatusMismatch
    | RenderingMismatch

/// Why a document, a value or a history was refused: the class, where (a `$`-rooted path into the
/// document, or the name of the argument), and a sentence for a person.
type Refusal = {
    Class: RefusalClass
    At: string
    Message: string
}

[<RequireQualifiedAccess>]
module RefusalClass =

    /// The class's wire spelling, as §3.6 and the corpus manifest write it.
    let code (c: RefusalClass) : string =
        match c with
        | RefusalClass.NotCanonical -> "not-canonical"
        | RefusalClass.ShapeInvalid -> "shape-invalid"
        | RefusalClass.KindUnknown -> "kind-unknown"
        | RefusalClass.VersionUnsupported -> "version-unsupported"
        | RefusalClass.DecimalInvalid -> "decimal-invalid"
        | RefusalClass.InstantInvalid -> "instant-invalid"
        | RefusalClass.TokenInvalid -> "token-invalid"
        | RefusalClass.IntervalInverted -> "interval-inverted"
        | RefusalClass.PeriodInverted -> "period-inverted"
        | RefusalClass.FactIdMismatch -> "fact-id-mismatch"
        | RefusalClass.UnitInvalid -> "unit-invalid"
        | RefusalClass.DisplayFormatInvalid -> "display-format-invalid"
        | RefusalClass.RegistryUnordered -> "registry-unordered"
        | RefusalClass.HistoryIdRepeated -> "history-id-repeated"
        | RefusalClass.SupersedesUnknown -> "supersedes-unknown"
        | RefusalClass.SupersedesCrossLineage -> "supersedes-cross-lineage"
        | RefusalClass.SupersedesNotLater -> "supersedes-not-later"
        | RefusalClass.SupersedesNotHead -> "supersedes-not-head"
        | RefusalClass.SupersedesMissing -> "supersedes-missing"
        | RefusalClass.ViewMismatch -> "view-mismatch"
        | RefusalClass.StatusMismatch -> "status-mismatch"
        | RefusalClass.RenderingMismatch -> "rendering-mismatch"

    /// Every class, in §3.6's order.
    let all: RefusalClass list = [
        RefusalClass.NotCanonical
        RefusalClass.ShapeInvalid
        RefusalClass.KindUnknown
        RefusalClass.VersionUnsupported
        RefusalClass.DecimalInvalid
        RefusalClass.InstantInvalid
        RefusalClass.TokenInvalid
        RefusalClass.IntervalInverted
        RefusalClass.PeriodInverted
        RefusalClass.FactIdMismatch
        RefusalClass.UnitInvalid
        RefusalClass.DisplayFormatInvalid
        RefusalClass.RegistryUnordered
        RefusalClass.HistoryIdRepeated
        RefusalClass.SupersedesUnknown
        RefusalClass.SupersedesCrossLineage
        RefusalClass.SupersedesNotLater
        RefusalClass.SupersedesNotHead
        RefusalClass.SupersedesMissing
        RefusalClass.ViewMismatch
        RefusalClass.StatusMismatch
        RefusalClass.RenderingMismatch
    ]

    /// The class a wire spelling names, or `None`.
    let tryParse (code': string) : RefusalClass option =
        all |> List.tryFind (fun c -> code c = code')

[<RequireQualifiedAccess>]
module Refusal =

    /// A refusal of class `c` at `at`.
    let make (c: RefusalClass) (at: string) (message: string) : Refusal = {
        Class = c
        At = at
        Message = message
    }

    /// `class: at: message`, the form a log line or an exception carries.
    let describe (r: Refusal) : string =
        RefusalClass.code r.Class + ": " + r.At + ": " + r.Message
