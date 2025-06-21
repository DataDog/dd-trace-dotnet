// <copyright file="GetRecordsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;

/// <summary>
///     AWSSDK.Kinesis GetRecordsAsync CallTarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "AWSSDK.Kinesis",
    TypeName = "Amazon.Kinesis.AmazonKinesisClient",
    MethodName = "GetRecordsAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.Kinesis.Model.GetRecordsResponse]",
    ParameterTypeNames = new[] { "Amazon.Kinesis.Model.GetRecordsRequest", ClrNames.CancellationToken },
    MinimumVersion = "3.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AwsKinesisCommon.IntegrationName)]
[Browsable(browsable: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetRecordsAsyncIntegration
{
    private const string Operation = "GetRecords";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GetRecordsAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TGetRecordsRequest>(TTarget instance, TGetRecordsRequest request, CancellationToken cancellationToken)
        where TGetRecordsRequest : IGetRecordsRequest, IDuckType
    {
        if (request.Instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Consumer, parentContext: null, out var tags);

        var streamName = AwsKinesisCommon.StreamNameFromARN(request.StreamARN);
        if (tags is not null && streamName is not null)
        {
            tags.StreamName = streamName;
        }

        return new CallTargetState(scope, streamName);
    }

    internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        where TResponse : IGetRecordsResponse, IDuckType
    {
        Console.WriteLine("[GetRecordsAsyncIntegration] OnAsyncMethodEnd called");

        if (response.Instance != null && response.Records is { Count: > 0 } && state is { State: not null, Scope.Span: { } span })
        {
            Console.WriteLine($"[GetRecordsAsyncIntegration] Processing {response.Records.Count} records");
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            if (dataStreamsManager is { IsEnabled: true })
            {
                var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:kinesis" };
                Console.WriteLine($"[GetRecordsAsyncIntegration] Edge tags: {string.Join(", ", edgeTags)}");

                foreach (var o in response.Records)
                {
                    var record = o.DuckCast<IRecord>();
                    if (record == null || record.Data == null)
                    {
                        Console.WriteLine("[GetRecordsAsyncIntegration] Skipping null record or data");
                        continue; // should not happen
                    }

                    Console.WriteLine("[GetRecordsAsyncIntegration] Processing record");
                    PathwayContext? parentPathway = null;
                    try
                    {
                        var jsonData = ContextPropagation.ParseDataObject(record.Data);
                        Console.WriteLine($"[GetRecordsAsyncIntegration] Parsed JSON data: {(jsonData == null ? "null" : $"count={jsonData.Count}")}");

                        if (jsonData != null && jsonData.TryGetValue(ContextPropagation.KinesisKey, out var datadogContext))
                        {
                            Console.WriteLine("[GetRecordsAsyncIntegration] Found _datadog key in JSON");
                            var adapter = new KinesisContextAdapter();
                            if (datadogContext is JObject jObject)
                            {
                                Console.WriteLine($"[GetRecordsAsyncIntegration] Converting JObject context");
                                var contextDict = jObject.ToObject<Dictionary<string, object>>();
                                if (contextDict != null)
                                {
                                    Console.WriteLine($"[GetRecordsAsyncIntegration] Converted to dictionary with {contextDict.Count} items");
                                    adapter.SetDictionary(contextDict);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[GetRecordsAsyncIntegration] Context is not a JObject, type: {datadogContext?.GetType().Name ?? "null"}");
                            }

                            parentPathway = dataStreamsManager.ExtractPathwayContext(adapter);
                            Console.WriteLine($"[GetRecordsAsyncIntegration] Extracted pathway context: {(parentPathway == null ? "null" : "success")}");
                        }
                        else
                        {
                            Console.WriteLine("[GetRecordsAsyncIntegration] No _datadog key found in JSON");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GetRecordsAsyncIntegration] Error extracting PathwayContext: {ex.Message}");
                        Log.Error(ex, "Error extracting PathwayContext from Kinesis record");
                    }

                    Console.WriteLine("[GetRecordsAsyncIntegration] Setting data streams checkpoint");
                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0, parentPathway);
                }
            }
            else
            {
                Console.WriteLine("[GetRecordsAsyncIntegration] DataStreamsManager is not enabled");
            }
        }
        else
        {
            Console.WriteLine("[GetRecordsAsyncIntegration] No records to process or invalid state");
        }

        state.Scope.DisposeWithException(exception);
        return response;
    }
}
