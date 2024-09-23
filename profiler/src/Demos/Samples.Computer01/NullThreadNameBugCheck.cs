// <copyright file="NullThreadNameBugCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Samples.Computer01
{
    // This test validate the fix of https://github.com/DataDog/dd-trace-dotnet/issues/4004
    // --> create threads to change their name to null and ""
    internal class NullThreadNameBugCheck : ScenarioBase
    {
        public override void OnProcess()
        {
            Console.WriteLine();
            Thread.Sleep(200);

            var t1 = new Thread(() =>
            {
                Console.WriteLine("--> Thread 1");
                Thread.Sleep(500);
                Thread.CurrentThread.Name = String.Empty;

                Thread.Sleep(10000);

                Console.WriteLine("<-- Thread 1");
            });
            var t2 = new Thread(() =>
            {
                Console.WriteLine("--> Thread 2");
                Thread.Sleep(500);
                Thread.CurrentThread.Name = null;

                Thread.Sleep(10000);

                Console.WriteLine("<-- Thread 2");
            });

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();

            Console.WriteLine("End of NullThreadNameBugCheck.");
        }
    }
}
