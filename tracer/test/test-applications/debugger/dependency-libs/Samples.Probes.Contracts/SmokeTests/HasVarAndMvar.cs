// <copyright file="HasVarAndMvar.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class HasVarAndMvar : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            new Test<Generic>().Method(new Generic());
        }

        public class Test<T>
            where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.Collections.Generic.List`1<Samples.Probes.Contracts.SmokeTests`1<!0>>", new[] { "!!0" })]
            public List<Test<T>> Method<TGeneric>(TGeneric k)
                where TGeneric : IGeneric
            {
                var @string = k.ToString();
                System.Console.WriteLine(@string);
                var kk = new List<Test<TGeneric>>() { new Test<TGeneric>() };
                System.Console.WriteLine(kk);
                var tt = new List<Test<T>>() { new Test<T>() };
                return tt;
            }
        }
    }
}
