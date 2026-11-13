// <copyright file="TestStack.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    internal static class TestStack
    {
        public static string Text(string function)
        {
            return "|lm:Tests |ns:Profiler |ct:Fixture |cg: |fn:" + function + " |fg: |sg:()";
        }

        public static StackFrame Frame(string function)
        {
            return new StackFrame(Text(function));
        }

        public static StackTrace Trace(params string[] functions)
        {
            var frames = new StackFrame[functions.Length];
            for (var i = 0; i < functions.Length; i++)
            {
                frames[i] = Frame(functions[i]);
            }

            return new StackTrace(frames);
        }
    }
}
