// <copyright file="NonSupportedInstrumentationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    public class NonSupportedInstrumentationTest : IRun
    {
        public void Run()
        {
            new GenericStructIsNotSupported<int>().MethodToInstrument(nameof(Run));
        }

        internal struct GenericStructIsNotSupported<T>
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData(expectedNumberOfSnapshots: 0)] // Should fail on instrumentation as we don't support the instrumentation of methods that reside inside an inner generic struct.
            public void MethodToInstrument(string callerName)
            {
                var arr = new[] { callerName, nameof(MethodToInstrument), nameof(SimpleTypeNameTest) };
                if (NoOp(arr).Length == arr.Length)
                {
                    throw new IntentionalDebuggerException("Same length.");
                }
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            private string[] NoOp(string[] arr) => arr;
        }
    }
}
