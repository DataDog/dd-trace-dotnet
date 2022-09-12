// <copyright file="HasLocalsAndArgumentsInGenericNestedType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class HasLocalsAndArgumentsInGenericNestedType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            new Test<GenericInstantiation>().Method(new GenericInstantiation(), 36);
        }

        public class Test<T>
            where T : new()
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "!0", "System.Int32" }, skipOnFramework: new string[] { "net6.0" })]
            public string Method(T genericVar, int age)
            {
                var genericVarToString = genericVar.ToString();
                var p2 = new Person(genericVarToString + "Simon", 30, new Address { HomeType = BuildingType.Hotel, Number = 3, Street = "Elsewhere" }, System.Guid.NewGuid(), null);
                var newT = (new T()).ToString();
                var p3 = new Person(newT + "Lucy", 7.5, new Address { HomeType = BuildingType.House, Number = 100, Street = "Here" }, System.Guid.NewGuid(), null);
                var children = new System.Collections.Generic.List<Person> { p2, p3 };
                var p = new Person(newT, age, new Address { HomeType = BuildingType.Cottage, Number = 17, Street = "Somewhere" }, System.Guid.NewGuid(), children);
                return $"Hello {p}!";
            }
        }

        private class GenericInstantiation
        {
            public GenericInstantiation()
            {
            }

            public override string ToString()
            {
                return $"{nameof(GenericInstantiation)}!";
            }
        }
    }
}
