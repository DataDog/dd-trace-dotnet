// <copyright file="OpenGenericMethodInDerivedGenericType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class OpenGenericMethodInDerivedGenericType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            new Test2<OpenGenericMethodInDerivedGenericType>().Method(new Generic(), new OpenGenericMethodInDerivedGenericType(), new Generic());
        }

        public class Test2<TGeneric2> : HasVarAndMvar.Test<Generic>
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "!!0", "!0", "Samples.Probes.Contracts.Shared.Generic" })]
            public string Method<T>(T k, TGeneric2 gen2, Generic gen)
            {
                var kToString = k.ToString();
                var gen2ToString = gen2.ToString();
                var genToString = gen.ToString();
                return kToString + gen2ToString + genToString;
            }
        }
    }
}
