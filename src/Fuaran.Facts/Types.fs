namespace Fuaran.Facts

// ─── The Fact kernel (FACT_WIRE.md §5, §7) ──────────────────────────────────────────────────────
//
// A **fact** is a verified assertion that a metric of a subject had a value over a period, as known
// at an instant, made by a stated method from stated inputs. The model is:
//
//   - **content-addressed** — a fact's id is the digest of its identity frame (`Fact.compute`), so
//     asserting the same tuple again is idempotent (L2) and a changed input yields a new fact whose
//     supersession edge is derived, never declared;
//   - **bitemporal** — `Period` is valid time, `AsOf` is transaction time; a forecast is a fact
//     whose valid time runs past its transaction time, derived from the timestamps (no flag);
//   - **append-only** — a correction is a new fact that supersedes the old one, never a mutation
//     (L1);
//   - **labelled at birth** — every fact carries a disclosure label from its first assertion. The
//     label is data: nothing in this library grants or withholds anything because of one.
//
// Generic: a metric is referenced by its registry id, a subject by an opaque hierarchy and path.

/// Reference to a registered metric by its stable id (a `MetricDefinition.Id`). A value wrapper, so
/// a metric reference is unambiguous in a signature.
type MetricRef =
    | MetricRef of string

    /// The metric id.
    member this.Value =
        let (MetricRef v) = this
        v

/// A node in a subject hierarchy (§5.2): `Hierarchy` is the hierarchy's id, `Path` the ordered
/// member ids from the root down to the node the fact is about. An empty `Path` is the hierarchy's
/// root — a total over every member.
type SubjectRef = { Hierarchy: string; Path: string list }

/// The valid-time extent of a fact (§5.4): half-open `[From, To)` in UTC, `From` equal to `To` an
/// instant-valued period. `Label` is a human tag (`Q2-2026`) scoped to the subject's calendar — never
/// compared across calendars, and not part of identity.
type TemporalExtent = {
    From: Instant
    To: Instant
    Label: string option
}

/// The value a fact carries (§5.3). A series is named by a content address, never inlined: a fact
/// is an assertion about data, not a copy of it. `Absent` records a queryable gap and why.
[<NoComparison>]
type FactValue =
    /// One value.
    | Scalar of DecimalText
    /// The bounds of a range, `low` <= `high`. Not a probabilistic claim.
    | Interval of low: DecimalText * high: DecimalText
    /// A body of points held elsewhere: `address` is `sha256:` and 64 lower-case hex digits over its
    /// stored bytes (a `Fuaran.Core` content address); `locator` is a host-specific hint for where
    /// they may be found, never part of identity.
    | Series of address: string * locator: string option
    /// Named buckets or quantiles to values; at least one.
    | Distribution of buckets: Map<string, DecimalText>
    /// A label.
    | Categorical of string
    /// The queryable absence of a value, and why.
    | Absent of reason: string

/// How a fact came to be (§5.5). The method's identity — not any transient payload — is part of the
/// id and of the lineage key.
type MethodRef =
    /// Produced by a deterministic operation at a version, with a hash over its parameters.
    | Computed of operationId: string * version: string * paramHash: string
    /// Asserted by a principal. The value is the input: its hash enters the id (§4.4), so asserting
    /// a different value supersedes within the principal's lineage, and two principals asserting
    /// differently are two competing facts.
    | HumanAsserted of principal: string
    /// Received from a peer under a certificate. The id is kept and re-derived, never re-minted.
    | Imported of certificateRef: string

/// A disclosure label (§5.8), carried on every fact from birth. **A label is data**: this library
/// reads, writes and preserves it, and attaches no rule about what any label permits.
type Disclosure =
    /// The fact may be shown beyond the host that holds it.
    | Surfaceable
    /// The fact is for computation and internal audit only.
    | Internal
    /// The fact is governed by the named policy.
    | Restricted of policyRef: string

/// How sure the producer is, and where the number came from (§5.7). Carried, never propagated.
[<NoComparison>]
type Confidence = {
    Value: DecimalText
    Basis: string option
}

/// The provenance a fact carries (§5.6).
type Evidence = {
    /// The result the fact was computed from, if any.
    ResultRef: string option
    /// The inputs, in the producer's order. A body of data is named in the content-hash form
    /// `<algorithm>:<hex>` (`InputHash`); other entries (a run token) are any non-empty string. The
    /// identity frame sorts its own copy.
    InputHashes: string list
    /// What caused the fact to be computed, if anything.
    TriggerRef: string option
}

/// A verified assertion about a (subject, metric, period). Immutable: a correction is a new fact
/// with a later `AsOf` and a derived `Supersedes` edge.
[<NoComparison>]
type Fact = {
    /// The id: `Fact.computeFor` of the fact's own members. Verified on read, never trusted.
    FactId: string
    Subject: SubjectRef
    Metric: MetricRef
    Value: FactValue
    /// Valid time: the period the value describes.
    Period: TemporalExtent
    /// Transaction time: when the fact was accepted.
    AsOf: Instant
    Method: MethodRef
    Evidence: Evidence
    Confidence: Confidence option
    /// The id of the fact this one replaced in its lineage — derived by the accepting host from the
    /// lineage's head (`History.accept`), never supplied by a producer. `None` for a lineage's first.
    Supersedes: string option
    Disclosure: Disclosure
}

// ─── Definitions as data (§7) ───────────────────────────────────────────────────────────────────

/// Which direction of movement in a metric's value is better.
type DirectionOfBetter =
    | HigherIsBetter
    | LowerIsBetter
    | Neutral

/// How long a fact carrying a metric stays fresh (§7.1, §6.6). Freshness is derived, never stored.
type StalenessPolicy =
    /// Fresh for `windowSeconds` (1 to 2147483647) after the fact's `AsOf`; stale after.
    | FreshFor of windowSeconds: int
    /// Fresh while the fact is its lineage's head.
    | UntilSuperseded
    /// Fresh while the fact is its lineage's head and no input it names has changed.
    | UntilUpstreamChange

/// How a metric's value at a parent subject relates to its children's (§7.1).
[<NoComparison>]
type RollUp =
    /// A parent's value is the sum of its children's, within `tolerance` (not negative), in the
    /// metric's unit.
    | Additive of tolerance: DecimalText
    /// A parent is not the sum of its children (a ratio, an average, a share).
    | NonAdditive

/// How a value is shown (§7.1), independent of any host's formatting library.
type DisplayStyle =
    /// The decimal text as carried.
    | Verbatim
    /// Rounded half away from zero to the format's fraction digits.
    | Number
    /// The value times 100, rounded likewise, with a percent sign.
    | Percent
    /// Rounded likewise, with the unit's currency. Only over a unit that is one currency.
    | Currency

/// A display format (§7.1): `Verbatim` has no fraction digits and no grouping; every other style
/// states its fraction digits (0 to 28). `Grouping` writes thousands separators.
type DisplayFormat = {
    Style: DisplayStyle
    FractionDigits: int option
    Grouping: bool
}

/// A registered business quantity (§7.1). Identity is the stable `Id` (a definition id, never the
/// display `Name`).
[<NoComparison>]
type MetricDefinition = {
    /// `[a-z0-9][a-z0-9._-]*`.
    Id: string
    /// Display name; cosmetic.
    Name: string
    /// The unit, in `Fuaran.Core.Unit`'s algebra. A metric is in a currency exactly when its unit
    /// is that currency to the first power, optionally prefixed (`MetricUnit.currency`).
    Unit: Fuaran.Core.UnitOfMeasure
    /// A coarse grouping label (`currency`, `count`, `ratio`, `duration`). Comparability is the
    /// unit's, not this label's.
    Dimensionality: string
    Direction: DirectionOfBetter
    DisplayFormat: DisplayFormat
    Staleness: StalenessPolicy
    /// The operation that produces the metric, if declared.
    ProducingOperation: string option
    /// A method-identity selector naming the lineage a method-less read prefers
    /// (`CanonicalMethod.matches`).
    CanonicalMethod: string option
    /// Roll-up semantics; `None` is undeclared.
    RollUp: RollUp option
    /// Prose about what the metric means; never a figure.
    Context: string option
}

/// A registered subject hierarchy (§7.3).
type SubjectDefinition = {
    /// A definition id: the `Hierarchy` a `SubjectRef` names.
    Id: string
    /// Display name; cosmetic.
    Name: string
    /// The kind of node at each depth, root first; at least one.
    Levels: string list
    /// The calendar a period label is read in; `None` is the default calendar.
    Calendar: string option
}
