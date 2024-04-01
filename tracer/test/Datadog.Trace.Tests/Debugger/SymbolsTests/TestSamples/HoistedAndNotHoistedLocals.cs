// <copyright file="HoistedAndNotHoistedLocals.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class HoistedAndNotHoistedLocals
    {
        private readonly IService _service;

        public HoistedAndNotHoistedLocals(IService service)
        {
            _service = service;
        }

        internal interface IService
        {
            Task BookRoom(HoistedLocalsAndArgsInStateMachine.Person person, string s);
        }

        public async Task<HoistedLocalsAndArgsInStateMachine.Person> AssignRoom(string name, string room, string purpose)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("name can't be null");
            }

            var person = GetPersonById(name);
            int count = person.Name.Length;
            if (count > 4)
            {
                Console.WriteLine(count);
            }
            else
            {
                Console.WriteLine("less");
            }

            var address = person.Address;

            if (address == null)
            {
                address = new HoistedLocalsAndArgsInStateMachine.Address(room, 1);
            }

            if (purpose == "lecture")
            {
                await _service.BookRoom(person, room);
            }

            if (purpose == "practice")
            {
                await _service.BookRoom(person, room);
            }

            Console.WriteLine(count);

            return await GetCourseSchedule(name, address);
        }

        private async Task<HoistedLocalsAndArgsInStateMachine.Person> GetCourseSchedule(string name, HoistedLocalsAndArgsInStateMachine.Address address)
        {
            await Task.Yield();
            return new HoistedLocalsAndArgsInStateMachine.Person(name, 2, address);
        }

        private HoistedLocalsAndArgsInStateMachine.Person GetPersonById(string name)
        {
            return new HoistedLocalsAndArgsInStateMachine.Person(name, 1, null);
        }

        internal class Service : HoistedAndNotHoistedLocals.IService
        {
            public async Task BookRoom(HoistedLocalsAndArgsInStateMachine.Person person, string s)
            {
                Console.WriteLine(person);
                await Task.Yield();
                Console.WriteLine(s);
            }
        }
    }
}
