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
