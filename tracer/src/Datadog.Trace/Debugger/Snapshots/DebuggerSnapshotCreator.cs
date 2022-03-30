// <copyright file="DebuggerSnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal readonly ref struct DebuggerSnapshotCreator
    {
        private const string LoggerVersion = "2";
        private const string DDSource = "dd_debugger";
        private const string UnknownValue = "Unknown";

        private readonly JsonTextWriter _jsonWriter;
        private readonly StringBuilder _jsonUnderlyingString;

        public DebuggerSnapshotCreator()
        {
            _jsonUnderlyingString = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            _jsonWriter = new JsonTextWriter(new StringWriter(_jsonUnderlyingString));

            _jsonWriter.WriteStartObject();
        }

        internal void StartDebugger()
        {
            _jsonWriter.WritePropertyName("debugger");
            _jsonWriter.WriteStartObject();
        }

        internal void StartSnapshot()
        {
            _jsonWriter.WritePropertyName("snapshot");
            _jsonWriter.WriteStartObject();
        }

        internal void StartCapture()
        {
            _jsonWriter.WritePropertyName("captures");
            _jsonWriter.WriteStartObject();
        }

        internal void StartEntry()
        {
            _jsonWriter.WritePropertyName("entry");
            _jsonWriter.WriteStartObject();
        }

        internal void EndEntry()
        {
            // end arguments or locals
            _jsonWriter.WriteEndObject();
            // end entry
            _jsonWriter.WriteEndObject();
        }

        internal void StartReturn()
        {
            _jsonWriter.WritePropertyName("return");
            _jsonWriter.WriteStartObject();
        }

        internal void EndReturn()
        {
            // end arguments or locals
            _jsonWriter.WriteEndObject();
            // end return
            _jsonWriter.WriteEndObject();
            // end capture
            _jsonWriter.WriteEndObject();
        }

        internal DebuggerSnapshotCreator EndSnapshot()
        {
            _jsonWriter.WritePropertyName("language");
            _jsonWriter.WriteValue(TracerConstants.Language);

            _jsonWriter.WriteEndObject();
            return this;
        }

        internal DebuggerSnapshotCreator EndDebugger()
        {
            _jsonWriter.WriteEndObject();
            return this;
        }

        internal void CaptureInstance<TInstance>(TInstance instance, Type type)
        {
            DebuggerSnapshotSerializer.SerializeObjectFields(instance, type, _jsonWriter);
        }

        internal void CaptureArgument<TArg>(TArg argument, string name, bool isFirstArgument, bool shouldEndLocals)
        {
            StartLocalsOrArgs(isFirstArgument, shouldEndLocals, "arguments");
            DebuggerSnapshotSerializer.Serialize(argument, name, _jsonWriter);
        }

        internal void CaptureLocal<TLocal>(TLocal local, string name, bool isFirstLocal)
        {
            StartLocalsOrArgs(isFirstLocal, false, "locals");
            DebuggerSnapshotSerializer.Serialize(local, name, _jsonWriter);
        }

        internal void CaptureException(Exception ex)
        {
            _jsonWriter.WritePropertyName("throwable");
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("message");
            _jsonWriter.WriteValue(ex.Message);
            _jsonWriter.WritePropertyName("type");
            _jsonWriter.WriteValue(ex.GetType().FullName);
            _jsonWriter.WritePropertyName("stacktrace");
            _jsonWriter.WriteStartArray();
            foreach (var frame in ex.StackTrace?.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
            {
                _jsonWriter.WriteValue(frame);
            }

            _jsonWriter.WriteEndArray();
            _jsonWriter.WriteEndObject();
        }

        internal DebuggerSnapshotCreator AddProbeInfo(string probeId, string methodName, string type)
        {
            _jsonWriter.WritePropertyName("probe");
            _jsonWriter.WriteStartObject();

            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(probeId);

            _jsonWriter.WritePropertyName("location");
            _jsonWriter.WriteStartObject();

            _jsonWriter.WritePropertyName("method");
            _jsonWriter.WriteValue(methodName ?? UnknownValue);

            _jsonWriter.WritePropertyName("type");
            _jsonWriter.WriteValue(type ?? UnknownValue);

            _jsonWriter.WriteEndObject();
            _jsonWriter.WriteEndObject();

            return this;
        }

        internal DebuggerSnapshotCreator AddStackInfo(StackFrame[] stackFrames)
        {
            _jsonWriter.WritePropertyName("stack");
            _jsonWriter.WriteStartArray();

            foreach (var frame in stackFrames)
            {
                _jsonWriter.WriteStartObject();
                _jsonWriter.WritePropertyName("function");
                var frameMethod = frame.GetMethod();
                _jsonWriter.WriteValue($"{frameMethod?.DeclaringType?.FullName ?? UnknownValue}.{frameMethod?.Name ?? UnknownValue}");

                var fileName = frame.GetFileName();
                if (fileName != null)
                {
                    _jsonWriter.WritePropertyName("fileName");
                    _jsonWriter.WriteValue(frame.GetFileName());
                }

                _jsonWriter.WritePropertyName("lineNumber");
                _jsonWriter.WriteValue(frame.GetFileLineNumber());
                _jsonWriter.WriteEndObject();
            }

            _jsonWriter.WriteEndArray();

            return this;
        }

        internal DebuggerSnapshotCreator AddLoggerInfo(string name, string method)
        {
            _jsonWriter.WritePropertyName("logger");
            _jsonWriter.WriteStartObject();

            var thread = Thread.CurrentThread;
            _jsonWriter.WritePropertyName("thread_id");
            _jsonWriter.WriteValue(thread.ManagedThreadId);

            _jsonWriter.WritePropertyName("thread_name");
            _jsonWriter.WriteValue(thread.Name);

            _jsonWriter.WritePropertyName("version");
            _jsonWriter.WriteValue(LoggerVersion);

            _jsonWriter.WritePropertyName("name");
            _jsonWriter.WriteValue(name);

            _jsonWriter.WritePropertyName("method");
            _jsonWriter.WriteValue(method);

            _jsonWriter.WriteEndObject();

            return this;
        }

        internal DebuggerSnapshotCreator AddGeneralInfo(TimeSpan? duration, string service, string traceId, string spanId)
        {
            _jsonWriter.WritePropertyName("service");
            _jsonWriter.WriteValue(service ?? UnknownValue);

            _jsonWriter.WritePropertyName("ddsource");
            _jsonWriter.WriteValue(DDSource);

            _jsonWriter.WritePropertyName("duration");
            _jsonWriter.WriteValue(duration.HasValue ? duration.Value.Milliseconds : UnknownValue);

            // todo
            _jsonWriter.WritePropertyName("ddtags");
            _jsonWriter.WriteValue(UnknownValue);

            _jsonWriter.WritePropertyName("trace_id");
            _jsonWriter.WriteValue(traceId);

            _jsonWriter.WritePropertyName("span_id");
            _jsonWriter.WriteValue(spanId);

            return this;
        }

        private void StartLocalsOrArgs(bool isFirstLocalOrArg, bool shouldEndObject, string name)
        {
            if (shouldEndObject)
            {
                _jsonWriter.WriteEndObject();
            }

            if (isFirstLocalOrArg)
            {
                _jsonWriter.WritePropertyName(name);
                _jsonWriter.WriteStartObject();
            }
        }

        internal string GetSnapshotJson()
        {
            _jsonWriter.WriteEndObject();
            return StringBuilderCache.GetStringAndRelease(_jsonUnderlyingString);
        }

        internal void Dispose()
        {
            _jsonWriter?.Close();
        }
    }
}
