// <copyright file="SnapshotSlicer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class SnapshotSlicer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerSink));

        private readonly int _maxDepth;
        private readonly int _maxSnapshotSize;

        private SnapshotSlicer(int maxDepth, int maxSnapshotSize)
        {
            _maxSnapshotSize = maxSnapshotSize;
            _maxDepth = maxDepth;
        }

        public static SnapshotSlicer Create(DebuggerSettings settings, int maxSnapshotSize = 1 * 1024 * 1024)
        {
            return new SnapshotSlicer(settings.MaximumDepthOfMembersToCopy, maxSnapshotSize);
        }

        public string SliceIfNeeded(string probeId, string snapshot)
        {
            var payloadSize = Encoding.UTF8.GetByteCount(snapshot);
            if (payloadSize < _maxSnapshotSize)
            {
                return snapshot;
            }

            try
            {
                return SliceSnapshot(snapshot, probeId, payloadSize);
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to fit snapshot with probe id {probeId} due to exception", e);
                return snapshot;
            }
        }

        private string SliceSnapshot(string snapshot, string probeId, int payloadSize)
        {
            var maxDepth = _maxDepth;
            var maxFieldDepth = 0;

            while (maxDepth > 0 && payloadSize >= _maxSnapshotSize)
            {
                Log.Information($"Trying to slice snapshot with probe id {probeId} by removing {maxDepth} `field` depth property");

                var fieldDepth = 0;
                var skipFields = false;
                var stringBuilder = StringBuilderCache.Acquire(payloadSize);

                using var jsonReader = new JsonTextReader(new StringReader(snapshot));
                using var jsonWriter = new JsonTextWriter(new StringWriter(stringBuilder));

                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName)
                    {
                        if (jsonReader.Value?.ToString() == "fields")
                        {
                            fieldDepth++;

                            if (fieldDepth == maxDepth)
                            {
                                skipFields = true;
                            }

                            maxFieldDepth = Math.Max(maxFieldDepth, fieldDepth);
                        }
                    }

                    if (skipFields)
                    {
                        jsonReader.Skip();
                        skipFields = false;
                        fieldDepth--;
                    }
                    else
                    {
                        jsonWriter.WriteToken(jsonReader, false);
                    }
                }

                snapshot = StringBuilderCache.GetStringAndRelease(stringBuilder);
                payloadSize = Encoding.UTF8.GetByteCount(snapshot);
                maxDepth = Math.Min(maxFieldDepth, maxDepth - 1);
            }

            Log.Information(
                payloadSize >= _maxSnapshotSize
                    ? $"Failed to fit snapshot with probe id {probeId} size to the {_maxSnapshotSize}"
                    : $"Succeed to fit snapshot with probe id {probeId} size to the {_maxSnapshotSize}");

            return snapshot;
        }
    }
}
