namespace Fuaran.Facts

/// The laws a history of facts obeys (FACT_WIRE.md §6): L1 append-only, L2 identity determinism,
/// L3 supersession shape, and L4 as-of visibility. A history is the list of facts a host accepted,
/// in the order it accepted them.
[<RequireQualifiedAccess>]
module History =

    let private refuse c at msg = Error(Refusal.make c at msg)

    /// The head of `fact`'s lineage in `history` — the last fact accepted into it — if it has one.
    let headOf (history: Fact list) (fact: Fact) : Fact option =
        let key = Fact.lineageKey fact
        history |> List.filter (fun g -> Fact.lineageKey g = key) |> List.tryLast

    /// Checks L1 within the history (an id appears once), L2 (every id recomputes) and L3 (each
    /// fact supersedes exactly its lineage's head at its acceptance, at a strictly later `AsOf`), or
    /// refuses with the class the first broken law names. `at` names the history in a refusal.
    let checkAt (at: string) (facts: Fact list) : Result<unit, Refusal> =
        let rec go (i: int) (accepted: Fact list) (heads: Map<string, Fact>) (rest: Fact list) =
            match rest with
            | [] -> Ok()
            | f :: tail ->
                let where = sprintf "%s.facts[%d]" at i
                let computed = Fact.computeFor f

                if computed <> f.FactId then
                    refuse
                        RefusalClass.FactIdMismatch
                        (where + ".id")
                        (sprintf "states %s, the frame gives %s" f.FactId computed)
                elif accepted |> List.exists (fun g -> g.FactId = f.FactId) then
                    refuse RefusalClass.HistoryIdRepeated where (sprintf "id %s was already accepted" f.FactId)
                else
                    let key = Fact.lineageKey f
                    let current = heads |> Map.tryFind key

                    let verdict =
                        match f.Supersedes, current with
                        | Some s, _ ->
                            match accepted |> List.tryFind (fun g -> g.FactId = s) with
                            | None ->
                                refuse
                                    RefusalClass.SupersedesUnknown
                                    where
                                    (sprintf "supersedes %s, which no earlier fact is" s)
                            | Some p when not (Fact.sameLineage p f) ->
                                refuse RefusalClass.SupersedesCrossLineage where "supersedes a fact of another lineage"
                            | Some p when not (f.AsOf > p.AsOf) ->
                                refuse
                                    RefusalClass.SupersedesNotLater
                                    where
                                    (sprintf
                                        "asOf %s does not exceed %s"
                                        (Instant.toWire f.AsOf)
                                        (Instant.toWire p.AsOf))
                            | Some p ->
                                match current with
                                | Some h when h.FactId = p.FactId -> Ok()
                                | Some h ->
                                    refuse
                                        RefusalClass.SupersedesNotHead
                                        where
                                        (sprintf "supersedes %s, and its lineage's head is %s" p.FactId h.FactId)
                                | None ->
                                    refuse
                                        RefusalClass.SupersedesNotHead
                                        where
                                        (sprintf "supersedes %s, and its lineage has no head" p.FactId)
                        | None, Some h ->
                            refuse
                                RefusalClass.SupersedesMissing
                                where
                                (sprintf "its lineage's head is %s, and it supersedes nothing" h.FactId)
                        | None, None -> Ok()

                    match verdict with
                    | Error e -> Error e
                    | Ok() -> go (i + 1) (accepted @ [ f ]) (heads |> Map.add key f) tail

        go 0 [] Map.empty facts

    /// `checkAt "history"`.
    let check (facts: Fact list) : Result<unit, Refusal> = checkAt "history" facts

    /// Accepts `fact` into `history` (§5.9, §6.3): verifies its id, derives `Supersedes` from its
    /// lineage's head, and returns the history with the accepted fact appended, and the fact as
    /// accepted. A fact whose id is already present changes nothing (L2): the history is returned
    /// as it was, with the fact it already holds. A fact whose `AsOf` does not exceed its lineage
    /// head's is refused (`supersedes-not-later`); any `Supersedes` the caller set is ignored.
    let accept (history: Fact list) (fact: Fact) : Result<Fact list * Fact, Refusal> =
        let computed = Fact.computeFor fact

        if computed <> fact.FactId then
            refuse RefusalClass.FactIdMismatch "fact.id" (sprintf "states %s, the frame gives %s" fact.FactId computed)
        else
            match history |> List.tryFind (fun g -> g.FactId = fact.FactId) with
            | Some existing -> Ok(history, existing)
            | None ->
                match headOf history fact with
                | Some h when not (fact.AsOf > h.AsOf) ->
                    refuse
                        RefusalClass.SupersedesNotLater
                        "fact.asOf"
                        (sprintf
                            "%s does not exceed its lineage head's %s"
                            (Instant.toWire fact.AsOf)
                            (Instant.toWire h.AsOf))
                | head ->
                    let accepted = {
                        fact with
                            Supersedes = head |> Option.map (fun h -> h.FactId)
                    }

                    Ok(history @ [ accepted ], accepted)

    /// Whether `later` extends `earlier` (L1 across two histories of one store): `earlier`'s facts are
    /// `later`'s first facts, in order. Facts compare as records, so a value changed in place breaks
    /// the extension even under an equal id.
    let extends (earlier: Fact list) (later: Fact list) : bool =
        List.length earlier <= List.length later
        && List.forall2 (fun a b -> a = b) earlier (List.truncate (List.length earlier) later)

    /// The ids visible at `at` (§6.4, L4), ascending ordinally: every fact accepted at or before `at`
    /// that no fact accepted at or before `at` supersedes.
    let visibleAt (history: Fact list) (at: Instant) : string list =
        history
        |> List.filter (fun f ->
            f.AsOf <= at
            && not (history |> List.exists (fun g -> g.Supersedes = Some f.FactId && g.AsOf <= at)))
        |> List.map (fun f -> f.FactId)
        |> Ordinal.sort

    /// Whether `fact` is its lineage's head in `history`: the last fact accepted into its lineage.
    let isHead (history: Fact list) (fact: Fact) : bool =
        match headOf history fact with
        | Some h -> h.FactId = fact.FactId
        | None -> false

/// Derived freshness of a fact: never a stored flag. `Stale` carries the instant the fact went
/// stale, so a caller can render "as of / stale since".
type FactFreshness =
    | Fresh
    | Stale of since: Instant

/// Freshness (§6.6), derived from a metric's staleness policy.
[<RequireQualifiedAccess>]
module Freshness =

    /// Freshness from a transaction time alone, given whether the fact is **current** — its
    /// lineage's head, and for `UntilUpstreamChange` with no input changed — at the instant `at`:
    ///
    ///  - `FreshFor w` — fresh while `at` <= `asOf` + `w` seconds; stale since `asOf` + `w`;
    ///  - `UntilSuperseded` / `UntilUpstreamChange` — fresh while current; otherwise stale since
    ///    `asOf`, the latest instant the fact itself carries.
    let deriveAt (policy: StalenessPolicy) (asOf: Instant) (isCurrent: bool) (at: Instant) : FactFreshness =
        match policy with
        | FreshFor window ->
            match Instant.tryAddSeconds (int64 window) asOf with
            | Some goesStaleAt -> if at <= goesStaleAt then Fresh else Stale goesStaleAt
            // A window that runs past the calendar's end never closes.
            | None -> Fresh
        | UntilSuperseded
        | UntilUpstreamChange -> if isCurrent then Fresh else Stale asOf

    /// Freshness of `fact` under `policy`.
    let derive (policy: StalenessPolicy) (fact: Fact) (isCurrent: bool) (at: Instant) : FactFreshness =
        deriveAt policy fact.AsOf isCurrent at

    /// Whether `fact` is fresh at `at`.
    let isFresh (policy: StalenessPolicy) (fact: Fact) (isCurrent: bool) (at: Instant) : bool =
        derive policy fact isCurrent at = Fresh
