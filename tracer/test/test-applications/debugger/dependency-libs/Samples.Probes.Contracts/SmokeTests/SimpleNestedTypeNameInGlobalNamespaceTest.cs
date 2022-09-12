// <copyright file="SimpleNestedTypeNameInGlobalNamespaceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    public class SimpleNestedTypeNameInGlobalNamespaceTest : IRun
    {
        public void Run()
        {
            new NestedType().MethodToInstrument(nameof(Run));
        }

        internal class NestedType
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.Void", new[] { "System.String" }, useFullTypeName: false)]
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
