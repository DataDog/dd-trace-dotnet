module Datadog.Trace.Tests.Properties.Tests.AgentWriterTests
open System
open System.Linq
open System.Threading.Tasks
open Datadog.Trace
open Datadog.Trace.Agent
open Datadog.Trace.Agent.MessagePack
open FsCheck.Xunit
open Moq
open FsCheck


// adapted from /test/Datadog.Trace.Tests/AgentWriterTests.cs - WriteTrace_2Traces_SendToApi

[<Property>]
let ``WriteTrace will send an all trace it receives to Api`` (tracerId: uint64) (spanId: uint64) =

    let writesArb = Gen.choose (0, 20) |> Arb.fromGen

    let trace = [| new Span(new SpanContext(tracerId, spanId), DateTimeOffset.UtcNow) |]

    let property writes =
        let expectedData = 
            let dataFromOneTrace = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, new FormatterResolverWrapper(SpanFormatterResolver.Instance))
            [| for _ in 1 .. writes do 
                yield! dataFromOneTrace |]

        let mutable tracesReceived = 0
        let mutable bytesReceived: byte[] = Array.zeroCreate 0

        let apiImpl =
            { new IApi with
                member x.SendTracesAsync(traces: ArraySegment<byte>, numberOfTraces: int) =
                    tracesReceived <- tracesReceived + numberOfTraces
                    bytesReceived <- Array.append bytesReceived (traces.Array.Skip(traces.Offset).Take(traces.Count).Skip(SpanBuffer.HeaderSize).ToArray())

                    Task.FromResult true }

        let agentWriter = new AgentWriter(apiImpl, statsd = null)

        async { for _ in 1 .. writes do
                    agentWriter.WriteTrace(trace)

                do! agentWriter.FlushTracesAsync() |> Async.AwaitTask // Force a flush to make sure the trace is written to the API

                do! agentWriter.FlushAndCloseAsync() |> Async.AwaitTask
                
                return 
                    tracesReceived = writes &&
                    bytesReceived = expectedData }

    Prop.forAll writesArb property
