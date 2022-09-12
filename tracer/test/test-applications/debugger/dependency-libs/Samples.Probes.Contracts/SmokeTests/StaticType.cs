// <copyright file="StaticType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 30)]
    public class StaticType : IRun
    {
        public void Run()
        {
            StaticTypeInner.Method("Last name");
        }

        public static class StaticTypeInner
        {
#pragma warning disable SA1401 // Fields should be private
            public static string StaticField = "Static Field";
#pragma warning restore SA1401 // Fields should be private

            public static string StaticProperty { get; } = "Static Property";

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "System.String" })]
            public static string Method(string lastName)
            {
                return lastName;
            }
        }
    }
}
