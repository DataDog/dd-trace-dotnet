// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.ServiceProcess;

namespace Datadog.Demos.WindowsService01
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var service = new DemoService1();
                service.CallStart(args);

                Console.WriteLine();
                Console.WriteLine($"{service.GetType().FullName} started.");
                Console.WriteLine("Press enter to stop.");
                Console.ReadLine();

                service.CallStop();

                service.Dispose();
                service = null;
            }
            else
            {
                ServiceBase[] servicesToRun;
                servicesToRun = new ServiceBase[]
                    {
                        new DemoService1()
                    };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}
