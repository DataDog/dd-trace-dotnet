// <copyright file="DebuggerSnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/*
 * The method in this type are called from IL code we inject into the instrumented method (via DebuggerInvoker).
 * The following is the expected order of method calls:
 * StartCapture
 *      -- StartEntry
 *          -- CaptureInstance (if not null)
 *          -- StartLocalsOrArgs (if any)
 *              -- CaptureLocal
 *          -- StartLocalsOrArgs (if any - also end locals if required)
 *              -- CaptureArgument
 *      -- EndEntry (also end arguments or locals if required)
 *      -- StartReturn
 *          -- CaptureInstance (if not null)
 *          -- CaptureException (if exit with exception)
 *          -- StartLocalsOrArgs (if any - including return value)
 *              -- CaptureLocal
 *          -- StartLocalsOrArgs (if any - also end locals if required)
 *              -- CaptureArgument
 *      EndReturn (also end arguments or locals if required and end capture)
 * AddProbeInfo - id & location
 * AddStackInfo - string array
 * AddThreadInfo - id & name
 * AddGeneralInfo
 *
 * Each captured object contains: 1. type 2. value 3. fields (if any) recursively with depth limit
 */

/*
* Example of snapshot for instance method with return value
* {
   "captures": {
       "entry": {
           "fields": {
               "type": "Samples.WebRequest.Greeter",
               "value": "Samples.WebRequest.Greeter",
               "fields": {
                   "LastGreetingTime": {
                       "type": "System.DateTime",
                       "value": "01/01/0001 0:00:00"
                   }
               }
           },
           "arguments": {
               "argument_0": {
                   "type": "System.String",
                   "value": "Robert"
               }
           }
       },
       "return": {
           "this": {
               "type": "Samples.WebRequest.Greeter",
               "value": "Samples.WebRequest.Greeter",
               "fields": {
                   "LastGreetingTime": {
                       "type": "System.DateTime",
                       "value": "17/02/2022 11:52:30"
                   }
               }
           }
           "arguments": {
               "argument_0": {
                   "type": "System.String",
                   "value": "Robert"
               }
           },
           "locals": {
               "@return": {
                   "type": "System.String",
                   "value": "Hello Robert (60)!"
               },
               "local_0": {
                   "type": "Samples.WebRequest.Person",
                   "value": "Samples.WebRequest.Person",
                   "fields": {
                       "_someField": {
                           "type": "System.Int32",
                           "value": "60"
                       },
                       "Name": {
                           "type": "System.String",
                           "value": "Robert"
                       },
                       "NestedObject": {
                           "type": "Samples.WebRequest.Address",
                           "value": "Samples.WebRequest.Address",
                           "fields": {
                               "Street": {
                                   "type": "System.String",
                                   "value": "Somewhere"
                               },
                               "Number": {
                                   "type": "System.Int32",
                                   "value": "17"
                               }
                           }
                       }
                       "Children": {
                           "type": "System.Collections.Generic.List`1[[Samples.WebRequest.Person]]",
                           "value": "count: 1",
                           "Collection": [
                               {
                                   "type": "Samples.WebRequest.Person",
                                   "value": "Samples.WebRequest.Person",
                                   "fields": {
                                       "_someField": {
                                           "type": "System.Int32",
                                           "value": "20"
                                       },
                                       "Name": {
                                           "type": "System.String",
                                           "value": "Simon"
                                       },
                                       "NestedObject": {
                                           "type": "Samples.WebRequest.Address",
                                           "value": "Samples.WebRequest.Address",
                                           "fields": {
                                               "Street": {
                                                   "type": "System.String",
                                                   "value": "Elsewhere"
                                               },
                                               "Number": {
                                                   "type": "System.Int32",
                                                   "value": "3"
                                               }
                                           }
                                       }
                                       "Children": {
                                           "type": "NA",
                                           "value": null
                                       }
                                   }
                               }
                           ]
                       }
                   }
               }
           }
       }
   },
   "probe": {
       "id": "3f5ed72b-86f5-4c33-bb24-0ab0b7cf568f",
       "location": {
           "method": "Greeting",
           "type": "Samples.WebRequest.Greeter"
       }
   },
   "stack": [
       {
            "method": "Greeting",
            "fileName": null,
            "lineNumber": 0
        },
        {
            "method": "MoveNext",
            "fileName": null,
            "lineNumber": 0
        }
   ],
   "thread": {
       "id": 13,
       "name": null
   }
}
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.SnapshotSerializer
{
    internal readonly ref struct DebuggerSnapshotCreator
    {
        private readonly ImmutableDebuggerSettings _debuggerSettings;
        private readonly JsonTextWriter _jsonWriter;
        private readonly StringBuilder _jsonUnderlyingString;

        public DebuggerSnapshotCreator()
        {
            _jsonUnderlyingString = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            _jsonWriter = new JsonTextWriter(new StringWriter(_jsonUnderlyingString));
            _debuggerSettings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromDefaultSource());
        }

        internal void StartCapture()
        {
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("captures");
        }

        internal void StartEntry()
        {
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("entry");
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
        }

        internal void EndReturn()
        {
            // end arguments or locals
            _jsonWriter.WriteEndObject();
            // end return
            _jsonWriter.WriteEndObject();
            // end captures
            _jsonWriter.WriteEndObject();
        }

        internal void CaptureInstance<TInstance>(TInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            _jsonWriter.WriteStartObject();
            DebuggerSnapshotSerializer.Clone(instance, _jsonWriter, "fields");
        }

        internal void CaptureArgument<TArg>(TArg argument, string name, bool isFirstArgument, bool shouldEndLocals)
        {
            StartLocalsOrArgs(isFirstArgument, shouldEndLocals, "arguments");
            DebuggerSnapshotSerializer.Clone(argument, _jsonWriter, name);
        }

        internal void CaptureLocal<TLocal>(TLocal local, string name, bool isFirstLocal)
        {
            StartLocalsOrArgs(isFirstLocal, false, "locals");
            DebuggerSnapshotSerializer.Clone(local, _jsonWriter, name);
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
            foreach (var frame in ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                _jsonWriter.WriteValue(frame);
            }

            _jsonWriter.WriteEndArray();
            _jsonWriter.WriteEndObject();
        }

        internal void AddProbeInfo(in Guid probeId, StackFrame frame)
        {
            _jsonWriter.WritePropertyName("probe");
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(probeId);
            _jsonWriter.WritePropertyName("location");
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("method");
            var frameMethod = frame.GetMethod();
            _jsonWriter.WriteValue(frameMethod?.Name ?? "Unknown");
            _jsonWriter.WritePropertyName("type");
            _jsonWriter.WriteValue(frameMethod?.DeclaringType?.FullName ?? "Unknown");
            _jsonWriter.WriteEndObject();
            _jsonWriter.WriteEndObject();
        }

        internal void AddStackInfo(StackFrame[] stackFrames)
        {
            _jsonWriter.WritePropertyName("stack");
            _jsonWriter.WriteStartArray();
            foreach (var frame in stackFrames)
            {
                _jsonWriter.WriteStartObject();
                _jsonWriter.WritePropertyName("method");
                _jsonWriter.WriteValue(frame.GetMethod().Name);
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
        }

        internal void AddThreadInfo()
        {
            _jsonWriter.WritePropertyName("thread");
            _jsonWriter.WriteStartObject();
            var thread = Thread.CurrentThread;
            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(thread.ManagedThreadId);
            _jsonWriter.WritePropertyName("name");
            _jsonWriter.WriteValue(thread.Name);
            _jsonWriter.WriteEndObject();
        }

        internal void AddGeneralInfo(in Guid snapshotId, int duration)
        {
            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(snapshotId);

            _jsonWriter.WritePropertyName("language");
            _jsonWriter.WriteValue("dotnet");

            _jsonWriter.WritePropertyName("timestamp");
            _jsonWriter.WriteValue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            _jsonWriter.WritePropertyName("duration");
            _jsonWriter.WriteValue(duration);

            _jsonWriter.WritePropertyName("trace id");
            _jsonWriter.WriteValue(Guid.Empty);

            _jsonWriter.WritePropertyName("span id");
            _jsonWriter.WriteValue(Guid.Empty);

            _jsonWriter.WritePropertyName("version");
            _jsonWriter.WriteValue(_debuggerSettings.Version);

            // close snapshot
            _jsonWriter.WriteEndObject();
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
            return StringBuilderCache.GetStringAndRelease(_jsonUnderlyingString);
        }

        internal void Dispose()
        {
            _jsonWriter?.Close();
        }
    }
}
