/// Facts in the legacy saved shape, as an earlier generation of the kernel's own serialiser wrote them
/// (one of each value kind, a date with no zone, every disclosure label), each with the id that
/// implementation minted for it and the lineage key it indexed it by. `LegacyFact.read` must read
/// each back with that id and that key, so a store holding them adopts this package without
/// rewriting a fact or an index. Run on both legs.
module Fuaran.Facts.Tests.LegacyShape

open Fuaran.Facts

type Sample = {
    Name: string
    /// The saved fact, byte for byte as that serialiser wrote it.
    Saved: string
    /// The lineage key that implementation computed for it.
    LineageKey: string
}

let samples: Sample list = [
    {
        Name = "scalar"
        Saved =
            """{"FactId":"2035f6bff71dd60cfe15197b2392b640bd4eb11eeabcc7a125b26e90d607f018","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","run:7"],"TriggerRef":null},"Confidence":{"Value":0.9,"Basis":null},"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "scalar-no-zone"
        Saved =
            """{"FactId":"cb1f59f6b5f6b72df897c7e5c42827934cd50c90d3023bd6cc0eebbd0dd1b98d","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":-0.125},"Period":{"From":"2026-01-01T00:00:00.0000000","To":"2026-04-01T00:00:00.0000000","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Internal"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-a"
    }
    {
        Name = "interval"
        Saved =
            """{"FactId":"180dbe87e8fd01678eef989432d29bcaa18d69ef0dba8cba7333ecb23a8f2a75","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Interval":[1.0,2.50]},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":{"Restricted":"panel-terms"}}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-a"
    }
    {
        Name = "distribution"
        Saved =
            """{"FactId":"18605e5b283f463c35925b473aafa54ca77baec29bd208b7f6528169843c0615","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Distribution":{"p10":1.10,"p90":3}},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-b"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-b"
    }
    {
        Name = "series"
        Saved =
            """{"FactId":"3fa9c35e4b502a914d7c2466e857eb4b82b195e48e8dfd2d6395059bec81d433","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Series":"objects/throughput@v12"},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-b"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-b"
    }
    {
        Name = "series-computed"
        Saved =
            """{"FactId":"8deb83f3d1871e59a01fa8b686987d7937c67eef0c63e44e68e87a83e49cf7a3","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Series":"objects/throughput@v12"},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op","1","h"]},"Evidence":{"ResultRef":null,"InputHashes":["sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op:1:h"
    }
    {
        Name = "categorical"
        Saved =
            """{"FactId":"50e8b1174d915a13a36b455319c6dc59818ab9ff40f3b2482b76e9cc5120674a","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Categorical":"red"},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Imported":"cert:9"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|imported:cert:9"
    }
    {
        Name = "absent"
        Saved =
            """{"FactId":"ffb421a562b58717692d1f688724a4a4c8add402fccf772feac9745daa7c51b5","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Absent":"no data loaded"},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op","1",""]},"Evidence":{"ResultRef":null,"InputHashes":["sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op:1:"
    }
]

/// The id a saved fact states: the id its implementation minted.
let private statedId (saved: string) =
    let i = saved.IndexOf "\"FactId\":\"" + 10
    saved.Substring(i, 64)

/// The samples that do not read back with their own id and lineage key: `(name, why)`.
let failures () : (string * string) list =
    samples
    |> List.choose (fun s ->
        match LegacyFact.read s.Saved with
        | Error r -> Some(s.Name, "refused: " + Refusal.describe r)
        | Ok f when f.FactId <> statedId s.Saved -> Some(s.Name, "read with another id")
        | Ok f when Fact.computeFor f <> statedId s.Saved -> Some(s.Name, "its id does not recompute")
        | Ok f when Fact.lineageKey f <> s.LineageKey -> Some(s.Name, "lineage key " + Fact.lineageKey f)
        | Ok f ->
            match FactWire.writeFact f with
            | Error r -> Some(s.Name, "does not write: " + Refusal.describe r)
            | Ok _ -> None)

/// Facts at rest that carry a token the specification refuses (FACT_WIRE.md §3.5), one per token, as a
/// store kept them before the refusal: each with the id it was minted with and the lineage key it was
/// indexed by. `LegacyFact.read` must read each back with both unchanged, and `FactWire` must refuse to
/// write it, so that no new fact can carry one. The last sample combines several refused tokens in one
/// path, as a store that seeds awkward paths on purpose holds them.
let storedTokenSamples: Sample list = [
    {
        Name = "path-member-angle"
        Saved =
            """{"FactId":"7d1f25fb9c7c4ba382b2955369d6403968636a5efd6d608c18bb912d9d122c64","Subject":{"Hierarchy":"area","Path":["west>harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "path-member-pipe"
        Saved =
            """{"FactId":"a821c1a33378c0d9c3295ad2dfba66092e4fa221794e4c125067a3aa20e526b4","Subject":{"Hierarchy":"area","Path":["west","harbour|north"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|north|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "path-member-empty"
        Saved =
            """{"FactId":"b5758ea209200fab7ca353e99d562efa127b026d50a099907b5acd5f8e6936b0","Subject":{"Hierarchy":"area","Path":["west",""]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "hierarchy-slash"
        Saved =
            """{"FactId":"bcae19dfcfe46a7a49165a93ac65460cc065c807f11e3ea98be7ae2dca0d6ddb","Subject":{"Hierarchy":"area/coast","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/coast/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "hierarchy-pipe"
        Saved =
            """{"FactId":"5cb078fba865213ac77d3185a4c3d4107f02982cdb807361e4316737a1181c99","Subject":{"Hierarchy":"area|coast","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area|coast/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "metric-pipe"
        Saved =
            """{"FactId":"482b41a6f11373c33de4c223fc7850ed7cb76c947ca7155657b59bf9a989e9c6","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput|net"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|net|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
    {
        Name = "operation-colon"
        Saved =
            """{"FactId":"8205019c1b362a9ae9e8b081119a1930e73420dd1ce684fef55b77ff45dc5215","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op:sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op:sum:v1:p0"
    }
    {
        Name = "version-colon"
        Saved =
            """{"FactId":"c0942c91b98c0b1733687bc42ed210d96d24cd262b7b9e28a0cc770445dc27b4","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1:2","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:2:p0"
    }
    {
        Name = "bucket-key-equals"
        Saved =
            """{"FactId":"fa81c56295eaa04af579e4382b43ea3c9d1058803e3a2fc347e0f1376cb619c3","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Distribution":{"p=10":1.10,"p90":3}},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-a"
    }
    {
        Name = "bucket-key-semicolon"
        Saved =
            """{"FactId":"f442f29ef3a8f3dfbacfe01c4b448ec5a47221ea94a5954b81ad2a245b7c8d0f","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Distribution":{"p10;low":1.10,"p90":3}},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/west>harbour|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|asserted:analyst-a"
    }
    {
        Name = "awkward-path"
        Saved =
            """{"FactId":"7cb13279a995df87dd1602f0f74dd1044d6ac7a941287b43e0011a53a8ccae97","Subject":{"Hierarchy":"area","Path":["we>ird|path","","x>y"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        LineageKey =
            "area/we>ird|path>>x>y|throughput|2026-01-01T00:00:00.0000000Z..2026-04-01T00:00:00.0000000Z|computed:op.sum:v1:p0"
    }
]

/// Two facts at rest that a tolerated token makes render alike: the same lineage key and, where
/// `SameId` says so, the same id. In four of the pairs the second fact is one a writer accepts today.
type Ambiguity = {
    Name: string
    First: string
    Second: string
    SameId: bool
}

let ambiguities: Ambiguity list = [
    {
        Name = "path-member-angle"
        First =
            """{"FactId":"7d1f25fb9c7c4ba382b2955369d6403968636a5efd6d608c18bb912d9d122c64","Subject":{"Hierarchy":"area","Path":["west>harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"7d1f25fb9c7c4ba382b2955369d6403968636a5efd6d608c18bb912d9d122c64","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = true
    }
    {
        Name = "path-member-empty"
        First =
            """{"FactId":"b5758ea209200fab7ca353e99d562efa127b026d50a099907b5acd5f8e6936b0","Subject":{"Hierarchy":"area","Path":["west",""]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"b5758ea209200fab7ca353e99d562efa127b026d50a099907b5acd5f8e6936b0","Subject":{"Hierarchy":"area","Path":["west>"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = true
    }
    {
        Name = "hierarchy-slash"
        First =
            """{"FactId":"4d583e7388e6e7b2aa960e5fd80a58d03689f90fb8da1db01832e5951b9cda03","Subject":{"Hierarchy":"area/coast","Path":["west"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"4d583e7388e6e7b2aa960e5fd80a58d03689f90fb8da1db01832e5951b9cda03","Subject":{"Hierarchy":"area","Path":["coast/west"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = true
    }
    {
        Name = "lineage-separator"
        First =
            """{"FactId":"482b41a6f11373c33de4c223fc7850ed7cb76c947ca7155657b59bf9a989e9c6","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput|net"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"b5c30cb693412ccf6cf86e027742dfd307c00468bbd6e9c6d7b25ad683b4d5a6","Subject":{"Hierarchy":"area","Path":["west","harbour|throughput"]},"Metric":{"MetricRef":"net"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op.sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = false
    }
    {
        Name = "operation-colon"
        First =
            """{"FactId":"8205019c1b362a9ae9e8b081119a1930e73420dd1ce684fef55b77ff45dc5215","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op:sum","v1","p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"8205019c1b362a9ae9e8b081119a1930e73420dd1ce684fef55b77ff45dc5215","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Scalar":1234.50},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"Computed":["op","sum","v1:p0"]},"Evidence":{"ResultRef":null,"InputHashes":["run:7"],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = true
    }
    {
        Name = "bucket-key"
        First =
            """{"FactId":"72ccc942bdad7f6c4158e35ae00b8b975f118a89a36718ac0da22a79b321ca65","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Distribution":{"p10=1.10;p90":3}},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        Second =
            """{"FactId":"72ccc942bdad7f6c4158e35ae00b8b975f118a89a36718ac0da22a79b321ca65","Subject":{"Hierarchy":"area","Path":["west","harbour"]},"Metric":{"MetricRef":"throughput"},"Value":{"Distribution":{"p10":1.10,"p90":3}},"Period":{"From":"2026-01-01T00:00:00.0000000Z","To":"2026-04-01T00:00:00.0000000Z","Label":"Q1-2026"},"AsOf":"2026-04-02T06:30:00.5000000Z","Method":{"HumanAsserted":"analyst-a"},"Evidence":{"ResultRef":null,"InputHashes":[],"TriggerRef":null},"Confidence":null,"Supersedes":null,"Disclosure":"Surfaceable"}"""
        SameId = true
    }
]

/// The stored-token samples that do not read back as stored, or that a writer accepts: `(name, why)`.
let storedTokenFailures () : (string * string) list =
    storedTokenSamples
    |> List.choose (fun s ->
        match LegacyFact.read s.Saved with
        | Error r -> Some(s.Name, "refused: " + Refusal.describe r)
        | Ok f when f.FactId <> statedId s.Saved -> Some(s.Name, "read with another id")
        | Ok f when Fact.computeFor f <> statedId s.Saved -> Some(s.Name, "its id does not recompute")
        | Ok f when Fact.lineageKey f <> s.LineageKey -> Some(s.Name, "lineage key " + Fact.lineageKey f)
        | Ok f ->
            match FactWire.writeFact f with
            | Ok _ -> Some(s.Name, "writes: a new fact could carry the token")
            | Error _ -> None)

/// The ambiguous pairs whose facts do not both read back, each as stored and distinct from the other,
/// with the shared lineage key (and id, where stated): `(name, why)`.
let ambiguityFailures () : (string * string) list =
    ambiguities
    |> List.choose (fun a ->
        match LegacyFact.read a.First, LegacyFact.read a.Second with
        | Error r, _
        | _, Error r -> Some(a.Name, "refused: " + Refusal.describe r)
        | Ok f, Ok s when f.FactId <> statedId a.First || s.FactId <> statedId a.Second ->
            Some(a.Name, "read with another id")
        | Ok f, Ok s when f = s -> Some(a.Name, "the two read as one fact")
        | Ok f, Ok s when Fact.lineageKey f <> Fact.lineageKey s -> Some(a.Name, "the lineage keys differ")
        | Ok f, Ok s when (f.FactId = s.FactId) <> a.SameId ->
            Some(a.Name, sprintf "same id is %b" (f.FactId = s.FactId))
        | Ok _, Ok _ -> None)
