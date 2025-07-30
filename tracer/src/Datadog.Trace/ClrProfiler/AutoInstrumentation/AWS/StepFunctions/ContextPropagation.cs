// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Text;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    internal static class ContextPropagation
    {
        private const string StepFunctionsKey = "_datadog";

        public static void InjectContextIntoInput<TClientMarker, TExecutionRequest>(Tracer tracer, TExecutionRequest carrier, PropagationContext context)
            where TExecutionRequest : IContainsInput
        {
            // Inject the tracing headers
            var input = carrier.Input;
            if (input == null)
            {
                return;
            }

            Inject<TClientMarker>(tracer, context, ref input);
            carrier.Input = input;
        }

        private static void Inject<TExecutionRequest>(Tracer tracer, PropagationContext context, ref string input)
        {
            var sb = Util.StringBuilderCache.Acquire();
            sb.Append(input);
            // Ensure the input is a JSON object
            if (sb[0] != '{' || sb[sb.Length - 1] != '}')
            {
                return;
            }

            sb.Remove(sb.Length - 1, 1); // Remove closing brace "}"
            bool isEmpty = sb.Length == 1; // After removing the closing brace, check if it's an empty object
            if (!isEmpty)
            {
                sb.Append(','); // Add a comma if it's not an empty object
            }

            sb.AppendFormat(" \"{0}\": {{", StepFunctionsKey); // Add _datadog:" {
            tracer.TracerManager.SpanContextPropagator.Inject(context, sb, default(StringBuilderCarrierSetter));
            sb.Remove(sb.Length - 1, 1); // remove trailing comma
            sb.Append("}}"); // re-add both closing braces one for original JSON and one for context
            input = Util.StringBuilderCache.GetStringAndRelease(sb);
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.AppendFormat("\"{0}\":\"{1}\",", key, value);
            }
        }
    }
}
