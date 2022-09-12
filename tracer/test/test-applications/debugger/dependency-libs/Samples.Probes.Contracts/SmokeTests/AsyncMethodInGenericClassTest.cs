// <copyright file="AsyncMethodInGenericClassTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    public class AsyncMethodInGenericClassTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            var person = new Person("Harry", 28, new Address(), Guid.Empty, null);
            await new GenericClass<Person>().Run(person);
            var address = new Address { City = new Place { Name = "Some Place", Type = PlaceType.City }, HomeType = BuildingType.Cottage, Number = 99, Street = "Wall" };
            await new GenericClass<Address>().Run(address);
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class GenericClass<T>
#pragma warning restore SA1402 // File may only contain a single type
    {
        public async Task Run(T t)
        {
            var def = default(T);
            await Task.Yield();
            Console.WriteLine(def);
        }
    }
}
