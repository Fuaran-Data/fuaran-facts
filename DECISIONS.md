# Decisions

The record of why this package is the way it is. Each entry is numbered once and never renumbered; a later
decision that changes an earlier one says so and cites it.

---

## D1 — The repository: public at the cut, Apache-2.0 (Phase 8, 2026-11-04)

The repository is public from its first commit (operator, 2026-11-03), under Apache-2.0 with a `NOTICE`, on
`main`. It carries a `README.md` and this record and no private working instructions. The gate's last stage
(`run.ps1`, "boundary") sweeps every tracked file and every commit message for the names a public repository of
this family must never carry, holding the list as SHA-256 digests so that the gate does not publish what it
protects; the list is the Fact wire specification's, which guards the same boundary.

## D2 — A lift, not a rewrite (Phase 8, 2026-11-04)

`Fuaran.Facts` is the Fact kernel lifted from the implementation whose stores already hold fact ids, not written
afresh: the code that minted every stored id is the code that keeps minting it. What came across is the kernel the
specification describes — the record and its value, method, extent, evidence, confidence and disclosure label;
the identity frame, the value hash and the effective inputs; the input-hash form; the lineage key, forecastness and
freshness; the canonical-method selector; the currency-symbol table; and metric and subject definitions as data.
What stayed with that implementation is everything about storing, guarding and showing facts: stores and their
queries, disclosure enforcement (a viewer, an ordering of labels, a taint index, egress mapping), population,
aggregate and plan queries, publication, pipeline tools, and the recompute policy a host runs when an input
changes (the specification's D15).

Three things changed on the way across, each because the specification now says so:

1. **A quantity is decimal text and an instant is ticks** (D6), not a platform decimal and a platform date.
2. **A series names a content address, with an optional locator** (`Series of address * locator`), where the
   value used to be a host's own version key. A `humanAsserted` series fact therefore hashes `series:` + the
   address (the specification's D1 ruling 3); computed and imported series facts are unaffected.
3. **A metric definition's unit is a `Fuaran.Core.Unit` value and its display format a neutral record** (D5),
   where both were free strings.

**Premise checked: the ids agree.** Before anything was written, the published build of the original
implementation was run over the specification's 21 identity vectors and reproduced every id; the test suite
pins 14 further same-input ids it minted (`tests/Fuaran.Facts.Tests/SameInputs.fs`), two of which its own suite
had pinned against an independent computation. Both legs of this repository's gate recompute all of them.

## D3 — Restore path: public, from nuget.org only (Phase 8, 2026-11-04)

Every dependency is published on nuget.org, so `nuget.config` clears every source and names nuget.org alone, and
`Directory.Build.props` declares `<FuaranRestorePath>public</FuaranRestorePath>`. An outside clone restores
exactly what the gate restores. The `Fuaran.Core` packages are pinned through ONE property,
`FuaranCoreVersion` (0.40.3), because they are produced by one repository at one version.

The library takes `FSharp.Core`, `Fuaran.Core.Text` (the pure SHA-256 and the UTF-8 encoder) and
`Fuaran.Core.Unit` (the unit algebra), and nothing else. Both are `FSharp.Core`-only and Fable-clean, which is
what lets this package be both.

*Alternative.* A local folder feed for the development loop, as some siblings carry: rejected, because a
checkout's depth below a shared folder differs between a primary clone and a worktree, so no relative path is
right for both, and because nothing this package needs is unpublished.

## D4 — Not a cohort member (operator, 2026-11-03)

`Fuaran.Facts` is not a member of the coordinated release cohort of the central build packages, on the
reasoning `Fuaran.Eval.Core` recorded for itself: its consumers release on their own rhythm, and the version that
matters to each is the one whose types it compiles against. A cohort obligation would move this package for
reasons none of its consumers can see in its tree. It pins `Fuaran.Core` like any consumer, by one property,
raised when a reason to raise it exists. The producer-side rule is unaffected: a change to the public surface
advances `<Version>` by the draft-slot rule (`STABILITY.md`).

## D5 — Units and display formats: converted by table, refused rather than guessed (Phase 8, 2026-11-04)

The specification makes a metric's unit `Fuaran.Core.Unit`'s canonical text and its display format a neutral
record (its D5 and D6). Stored definitions written before it carry a free-text unit and a .NET numeric format
string, so `Legacy.unitOf` and `Legacy.displayFormatOf` convert them. Each conversion is a table, and anything the
table does not hold, and that is not already the thing it converts to, is refused, never guessed.

**Units.** A census of every metric definition stored in the source implementation and its consumers found these
units, and they convert as follows:

| Stored unit | Converts to | Why |
|---|---|---|
| `GBP` (and any three capitals) | `[GBP]` | an ISO 4217 code is a currency unit of its own dimension in Core |
| `count`, `items`, `units` | `1` | a dimensionless count is `1` (specification §7.2) |
| `ratio` | `1` | a dimensionless ratio |
| `1` | `1` | already canonical |
| `%` | `%` | already canonical unit text |

Anything else is read as unit text by `Fuaran.Core.Unit` (so `ms` and `[GBP]` pass), and refused (`unit-invalid`)
when it does not parse: the empty string, a lower-case code (`gbp`), and words outside the table such as
`bytes`, `tokens` or `B` (which Core reads as the bel, a logarithmic unit it refuses). The census also met such
strings in records that are not metric definitions (telemetry signals and unrelated fields); they are not
converted, and are listed only so the refusal is a known outcome rather than a surprise.

**Display formats.** `""` is `verbatim`. `N<d>` is a grouped `number`, `F<d>` an ungrouped one, `P<d>` a grouped
`percent` and `C<d>` a grouped `currency`, each with `d` fraction digits (two when omitted, the invariant
culture's default). A custom pattern (`#,##0.00`, `£#,##0`) is refused: its meaning belongs to a formatting
library, and a currency glyph written into a pattern is a second statement of the currency the unit already makes.
A `C` format over a unit that is not one currency (stored definitions carry `C0` over `count`) converts, and is
then refused by the definition's own check (`display-format-invalid`), because the specification refuses it.

**Rendering.** A value renders as its metric's format declares (`FactRendering.renderFor`), rounding half away
from zero on exact integer arithmetic, so the rendering is the same on every host. Two deliberate departures from
the .NET formats the stored strings named: a negative currency figure is written `-£1,235` rather than
`(£1,235)`, and a percent as `12.3%` rather than `12.3 %`. The specification fixes the number and leaves the
glyphs to the reader, and `RenderGlyphs` is where a reader states its own. *Superseded by D13:* the
rendering is now the specification's standard, glyphs included, and `RenderGlyphs` is gone.

## D6 — Decimal text and ticks, not platform types (Phase 8, 2026-11-04)

A quantity is `DecimalText`: the text, validated against §3.3, compared numerically by `DecimalText.compare` and
never normalised. An instant is `Instant`: ticks since 0001-01-01T00:00:00Z, the count a .NET `DateTime` holds.
Both are what the specification's bytes are, held exactly on every host: a JavaScript date has millisecond
resolution and a JavaScript number is not a decimal, so a platform type would have made the Fable build hash and
render differently from the .NET one. `DecimalText.ofDecimal` / `toDecimal` and `Instant.ofDateTime` /
`toDateTime` convert on .NET (the scale of a decimal, and the ticks of a date, are kept); an `Unspecified` date is
read as UTC, so an id is a function of the fact alone and never of the zone of the machine computing it.

## D7 — The frame is the specification's, hashed by Core's pure SHA-256 (Phase 8, 2026-11-04)

The identity frame is the length-prefixed `<utf16-length>:<field>` frame the specification states in full, not
`Fuaran.Core`'s content address (the specification's D1 ruling 2). The digest is `Fuaran.Core.Hash.sha256Hex`,
Core's FIPS 180-4 implementation in plain F#, rather than the platform's cryptography, because the platform's does
not compile under Fable and Core's is certified to give the same value on both pipelines. The fact id is bare
hex; a series address and a definition digest carry `sha256:`.

## D8 — Disclosure is labels only (Phase 8, 2026-11-04)

`Disclosure` is the three labels and nothing else. Any ordering of labels, any meet of two of them, and any
string form a host prints them in for its own audit trail is enforcement or presentation, which the
specification leaves to the host (§1, §5.8), so none of it is here.

## D9 — The writer reads back what it writes (Phase 8, 2026-11-04)

`FactWire.write` encodes a document and reads the text back under every rule a reader applies, returning the
refusal if there is one. A fact whose members moved without its id, a history that breaks a law, or a definition
outside its grammar is therefore refused where it is written, not where it is received. `FactWire.encode` is the
unchecked form. There is one reader, and the writer's check is that reader, so the two cannot disagree.

## D10 — The JavaScript leg is a gate stage that runs the corpus, not only a compile (Phase 8, 2026-11-04)

"It compiles under Fable" and "it gives the same answers under Fable" are different claims, and only the second
matters to a browser host. So the certification code (`Certify.fs`), the same-input ids and the rendering vectors
are written once, without a test framework, and run on both legs: by Expecto on .NET, and compiled by Fable and
run under node against the same corpus. The legs differ only in reading a file and parsing the manifest. Both
fail, never skip, when the corpus is absent, and the gate fails when node is. The leg installs no package, so
`run.ps1` resolves the node application directly and shells no `npm`, `npx` or `pnpm`.

Under Fable, `Fuaran.Core` is compiled from source and its internal names come into scope with it, so no file
here opens the `Fuaran.Core` namespace: one of its internal union cases would shadow this library's `Currency`.

## D11 — 0.1.0, a draft, published by tag through Trusted Publishing (Phase 8, 2026-11-04)

The first version is 0.1.0, a DRAFT slot until it is tagged (`STABILITY.md`). `.github/workflows/publish-packages.yml`
packs on a `v*` tag only, logs in to nuget.org through Trusted Publishing (OIDC, no stored key), refuses any
package id outside `Fuaran.Facts.*`, and pushes with `--skip-duplicate`; a branch push reaches no publish job. The
nuget.org policy (owner `Fuaran-Data`, repository `fuaran-facts`, workflow `publish-packages.yml`) is registered
by the operator before the first tag, and the tag is the operator's act. The `Fuaran.*` id prefix on nuget.org is
reserved to the operator's profile (checked 2026-11-04: `Fuaran.Core.*` and `Fuaran.Eval.*` are listed as
verified), and `Fuaran.Facts` was unclaimed.

## D12 — Supersession is derived at acceptance (Phase 8, 2026-11-04)

`History.accept` is the one place `Supersedes` is set: it verifies the incoming fact's id, makes it supersede its
lineage's current head (or nothing, for a lineage's first fact), and refuses a fact whose `AsOf` does not exceed
that head's, since a history with it would break L3. Accepting an id the history already holds changes nothing
(L2). Whatever `Supersedes` a caller set on a draft is ignored, because the specification derives it and never
takes it from a producer.

## D13 — 0.2.0: a host with stored facts adopts without migrating them, and one rendering is the standard (Phase 35, 2026-11-16)

0.1.0 was to be the kernel of the implementation whose stores already hold fact ids, lifted (D2). A probe run there
against 0.1.0 found its ids equal on every case and four differences that reach data at rest or text a reader sees:
the lineage key (0.1.0 framed it; that implementation's stores index a digest of the `|`-joined form), the saved
shape of a fact (a quantity as a JSON number, an instant as an ISO date, where 0.1.0's types serialise otherwise), a
series address (the store's own data version key, which 0.1.0's grammar refused), and the rendering of numbers. Three
operator rulings (2026-11-15) settled them, and the Fact wire specification moved first, in its revision 1.1 (its
D2). This entry records what the package did. **Premise checked at take:** each of the four differences reproduced
against the published 0.1.0, and the same probe run against this tree's build shows none.

**(1) The lineage key is the specification's `|`-joined form.** `Fact.lineageKey` joins the subject, metric, period
and method renderings with `|` (`Fact.LineageSeparator`), and `Fact.lineageDigest` is the SHA-256 of its UTF-8 bytes,
what a store indexes a lineage by. The reader refuses `|` inside a hierarchy, a path member and a metric id
(`token-invalid`), so no two lineages share a key; a method's members keep `|`. This supersedes D2's framed key.

**(2) A series address is typed: `ContentAddress` or `HostKey`.** `FactValue.Series` carries a `SeriesAddress`, and
`SeriesAddress.tryParse` tells the two forms apart by text alone, refusing text in a content-hash form that is not a
sha256 content address. A consumer can therefore tell an address it can verify from a key only its host resolves.
This supersedes D2's change 2 for stores that keyed their series by version.

**(3) The legacy saved shape is read, never written (`LegacyFact.read`).** The shape is what the earlier kernel's
general-purpose serialiser wrote: PascalCase members, a quantity as a JSON number whose token text is kept as the
decimal text, an instant with seven fractional digits and with or without `Z` (UTC either way; an offset is refused,
because that serialiser never wrote one), a union case as an object named for it. What is read is held to every
rule the specification's reader applies, through `FactWire.write`, so there is still one reader (D9): a legacy fact
whose id is not the digest of its own members is refused. `FactWire` is the only writer; nothing writes the legacy
shape, so a store converts to the specification's encoding whenever it next writes a fact, and never has to.
`tests/Fuaran.Facts.Tests/LegacyShape.fs` pins eight facts as that serialiser wrote them, one of each value kind, a
date with no zone and every disclosure label, each with the id and lineage key that implementation minted; both legs
read them back.
*Alternative.* Serialiser converters on the package's types keeping the legacy shape: it would make the legacy shape
a second wire, written as well as read, and tie the package to one serialiser.

**(4) The rendering is the specification's standard, rule 1 (its §10).** `FactRendering.renderFor` is the standard;
`FactRendering.Rule` is its number and `FactRendering.renderUnder` refuses a rule this version does not implement.
`RenderGlyphs` and `renderWith` are removed: the standard fixes the glyphs, and a hook for swapping them would be a
second renderer, which ruling 2 removes. A prefixed currency unit now shows its prefix after the number (`£12.50k`),
because 0.1.0's `£12.50` for `12.5` in `k[GBP]` misstated the amount. The rendering vectors that were this
repository's own (`RenderingVectors.fs`) are now the specification's `rendering` corpus family, certified on both
legs, so the file is gone. **Two renderings a deployed host shows change** from the format strings stored
definitions name: a negative currency figure is `-£1,235` where the platform's format gave `(£1,235)`, and a percent
is `12.3%` where it gave `12.3 %` (D5 already chose both for this package; the standard now makes them every host's).

**(5) Conversions from stored definitions (extends D5).**

| Stored | Converts to | Why |
|---|---|---|
| unit `bytes` | `1` | Core admits no unit of information; a byte count is a count (specification §7.2) |
| format `0`, `0.00`, … | `number`, ungrouped, as many digits as zeros | the one display format the pattern names |
| format `#,##0`, `#,##0.00`, … | `number`, grouped | likewise |
| either of the above followed by `%` | `percent`, grouped as the pattern is | likewise |
| any other custom pattern | refused, naming the standard format to write | its meaning is a formatting library's |

A refused pattern names its replacement: `£#,##0.00` is "write the currency as the metric's unit and use `C2`",
`#,###` is `N0`, `0.##` is `F2`. A `C` format over a unit that is not one currency (stored definitions carry `C0`
over `count`, which the platform's format rendered as `¤10`) converts and is then refused by the definition check, as
D5 said; the replacement is `N0`.

**(6) The recompute policy is the host's, carried beside the wire members.** `MetricDefinition.RecomputePolicy`
(`Eager`, `OnQuery`, `Manual`; `None` undeclared, which a host treats as `Manual`) lets a host use this package's
definition type alone (ruling 3). `FactWire` neither writes nor reads it, so a definition's bytes and digest do not
depend on it and two deployments that schedule recomputation differently still agree on what a metric means; a
definition read from the wire declares none, and its host sets its own (specification D2, ruling 5). Staleness stays
in whole seconds, and the unit and display format stay typed. **The mapping from a time span** is
`Legacy.windowSecondsOf`: a `System.TimeSpan` that is a whole number of seconds from 1 to 2147483647 converts to that
many seconds, and any other span is refused (`shape-invalid`) rather than rounded.

## D14 — 0.2.1: facts at rest read with every token they were stored with, and the Fable source compiles beside Core (Phase 37, 2026-11-16)

The first host to build on 0.2.0 found two faults its probe had not reached. **Premise checked at take:** both
reproduced in this repository before any change, and neither reproduces after it.

**(1) The legacy reader tolerates the refused tokens at rest, and nowhere else.** 0.2.0's `LegacyFact.read` held a
stored fact to every rule the wire applies, through `FactWire.write`, so a fact a store kept before the
specification refused a separator inside a token no longer read: a path member containing `>` or `|`, or empty; a
hierarchy containing `/` or `|`; a metric id containing `|`; a computed method's operation or version containing
`:`; a distribution key containing `=` or `;`. A host with such facts at rest could not adopt without rewriting
them, which D13 exists to prevent. The specification now states the tolerance (its §3.5 and D3, revision 1.2).

`LegacyFact.read` now checks what it read through `FactWire.checkStored`: `FactWire`'s own fact reader with the token
rules waived and nothing else, so the id is verified exactly as before and there is still one reader. The fact is
returned as stored, its id and lineage key unchanged; their bytes are not touched. `FactWire.read` and
`FactWire.write` are unchanged: every document and every writer still refuses each token, so a fact read with one
cannot be written, and a new fact cannot carry one. *Read, never write* now holds at the token level as well as at
the shape level.

**The ambiguity, per token** (§3.5 states it normatively; `LegacyShape.ambiguities` pins one pair per row on both
legs). The renderings are joined, not framed, so a tolerated token can make two different facts render alike:

| Token at rest | Two facts can share | And the other fact can be one a writer accepts |
|---|---|---|
| `>` in a path member | the id and the lineage key (`["a>b"]`, `["a";"b"]`) | yes |
| an empty path member | the id and the lineage key (`[""]`, `[]`; `["a";""]`, `["a>"]`) | yes (the empty path) |
| `/` in a hierarchy | the id and the lineage key (hierarchy `a/b` path `["c"]`; hierarchy `a` path `["b/c"]`) | yes |
| `\|` in a path member, hierarchy or metric id | the lineage key only, never the id | no |
| `:` in an operation or version | the id and the lineage key (`("a:b","c","d")`, `("a","b","c:d")`) | yes |
| `=` or `;` in a bucket key | the id of a `humanAsserted` fact, never the key | yes |

The reader returns each fact as stored and never merges two because they render alike: a reader that merges loses
data. What a host does is the specification's to say (§3.5): compare members before treating an id or key hit as
the same fact when either carries a tolerated token, and keep two facts as two; a host that wants the ambiguity
gone asserts its stored facts again under admitted tokens, as new lineages, by a migration of its own.

*Alternatives.* (a) A separate reader entry point for tolerant reads: two legacy readers, one of which a host would
call by mistake, for no gain; the legacy shape only ever holds data at rest. (b) Escape or re-frame the tokens:
changes the bytes of exactly the keys and ids a store already indexes. (c) A flag on `FactWire.read`: puts the
tolerance on the wire's own entry point, where a document reader could switch it on.

**(2) No qualified path into `Fuaran.Core` is written in an expression.** `Definitions.fs` wrote
`Fuaran.Core.Unit.render`, `.parse` and `.dimensionless`. A Fable consumer compiles this package's source and
`Fuaran.Core.Column`'s source into one project, and there `Fuaran.Core.Unit` in an expression resolves to the union
case `FieldChange.Unit` (declared with `RequireQualifiedAccess` in the same namespace) before the Unit module: 18
errors, at Column 0.42.0 and at 0.40.3 alike. Writing the namespace out does not help, and no expression can say
"the module". A module abbreviation's right-hand side resolves among modules and namespaces only, so each file that
calls into Core declares one (`UnitAlgebra` for `Fuaran.Core.Unit`; `CoreHash` for `Fuaran.Core.Hash` in `Text.fs`
and `Json.fs`, which no Core source shadows today but could) and calls through it. An abbreviation is always
private, so the public surface does not move. **The sweep:** every other reference into `Fuaran.Core` is in a type
position (`Fuaran.Core.UnitOfMeasure`), where a union case cannot resolve; `Fuaran.Core` is opened nowhere; and the
bare names this package uses resolve in `Fuaran.Facts` before any parent namespace.

**The gate holds it.** The existing JavaScript leg is now consumer-shaped: its Fable project
(`tests/Fuaran.Facts.Tests/Fable/`) references the published `Fuaran.Core.Column`, at the version a current consumer
takes (0.42.0, the project's `FuaranCoreColumnConsumerVersion`), so the package's source and Column's source are
compiled in one Fable project. Before this it compiled no Core source beside the package, which is why it could not
see the fault. Besides certifying the corpus, it now runs the unit functions it compiled and a value of Column's own
union. One project compiles one Column version, so the leg does not also compile the package beside Column at this
repository's own `FuaranCoreVersion` (0.40.3). The fault reproduced identically at both versions before the fix, and
the leg covers the version consumers take. `FuaranCoreVersion` stays 0.40.3: the fix needs nothing newer.

*Alternatives.* (a) Ask Core to rename the case or the module: a fix in another repository's public surface, for a
resolution rule any consumer source can trigger. (b) `open Fuaran.Core` and bare `Unit.render`: opens Core's
internal names under Fable, one of which (`Currency`) shadows this package's own, which is why nothing opens it.
