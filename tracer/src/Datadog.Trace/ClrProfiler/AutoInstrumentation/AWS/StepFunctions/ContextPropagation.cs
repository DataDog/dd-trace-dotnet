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

        public static void InjectContextIntoInput<TClientMarker, TExecutionRequest>(TExecutionRequest carrier, PropagationContext context)
            where TExecutionRequest : IContainsInput
        {
            // Inject the tracing headers
            var input = carrier.Input;
            if (input == null)
            {
                return;
            }

            Inject<TClientMarker>(context, ref input);
            carrier.Input = input;
        }

        private static void Inject<TExecutionRequest>(PropagationContext context, ref string input)
        {
            var sb = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);
            sb.Append(input);
            if (sb[sb.Length - 1] != '}')
            {
                return;
            }

            sb.Remove(sb.Length - 1, 1);
            sb.AppendFormat(", \"{0}\": {{", StepFunctionsKey);
            SpanContextPropagator.Instance.Inject(context, sb, default(StringBuilderCarrierSetter));
            sb.Remove(sb.Length - 1, 1); // remove trailing comma
            sb.Append("}}");
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
