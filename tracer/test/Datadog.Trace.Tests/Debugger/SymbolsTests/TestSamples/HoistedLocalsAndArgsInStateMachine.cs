// <copyright file="HoistedLocalsAndArgsInStateMachine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class HoistedLocalsAndArgsInStateMachine
    {
        private int intField;
        private Person personField;

        internal async Task Init()
        {
            intField = GetHashCode();
            personField = new Person("Me", 20, new Address("Add", 2));
            var person = new Person("You", 40, new Address("Ress", 4));
            await DoAsyncWork(intField, person);
        }

        private async Task DoAsyncWork(int id, Person person)
        {
            Console.WriteLine($"Before awaiting: {id}: {person}, Another {personField}");

            await Task.Delay(500);
            var localPerson = new Person("Local", 80, new Address("Local", 8));

            Console.WriteLine($"After awaiting: {id}: {person}, Another {personField}, Local: {localPerson}");

            try
            {
                await Task.Delay(500);
                Console.WriteLine($"After awaiting: {id}: {person}, Another {personField}, Local: {localPerson}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        internal record Person(string Name, int Age, Address Address)
        {
            public override string ToString()
            {
                return $"{Name}, {Age}, {Address}";
            }
        }

        internal record Address(string Name, int Number)
        {
            public override string ToString()
            {
                return $"{Name}, {Number}";
            }
        }
    }
}
