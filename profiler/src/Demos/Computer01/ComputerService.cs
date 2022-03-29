// <copyright file="ComputerService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Datadog.Demos.Computer01
{
#pragma warning disable CA1416 // Validate platform compatibility
    public class ComputerService
    {
        private Scenario _scenario;
        // Check case when a generic parameter is a reference type or not
        // private Computer<byte, KeyValuePair<char, KeyValuePair<int, KeyValuePair<float, double>>>> _computer;
        private Computer<byte, KeyValuePair<char, KeyValuePair<int, KeyValuePair<float, object>>>> _computer;
        private Task _computerTask;
        private GenericTypes _genericTypes;
        private SimpleWallTime _simpleWallTime;
        private PiComputation _piComputation;
        private FibonacciComputation _fibonacciComputation;
        private SleepManager _sleepManager;

        public void StartService(Scenario scenario, int nbThreads)
        {
            _scenario = scenario;

            Console.WriteLine(" ########### Starting.");

            switch (scenario)
            {
                case Scenario.All:
                    StartComputer();
                    StartGenerics();
                    StartSimpleWallTime();
                    StartPiComputation();
                    StartFibonacciComputation(nbThreads);
                    StartSleep(nbThreads);
                    break;

                case Scenario.Computer:
                    StartComputer();
                    break;

                case Scenario.Generics:
                    StartGenerics();
                    break;

                case Scenario.SimpleWallTime:
                    StartSimpleWallTime();
                    break;

                case Scenario.PiComputation:
                    StartPiComputation();
                    break;

                case Scenario.FibonacciComputation:
                    StartFibonacciComputation(nbThreads);
                    break;

                case Scenario.Sleep:
                    StartSleep(nbThreads);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), $"Unsupported scenario #{_scenario}");
            }
        }

        public void StopService()
        {
            switch (_scenario)
            {
                case Scenario.All:
                    StopComputer();
                    StopGenerics();
                    StopSimpleWallTime();
                    StopPiComputation();
                    StopFibonacciComputation();
                    StopSleep();
                    break;

                case Scenario.Computer:
                    StopComputer();
                    break;

                case Scenario.Generics:
                    StopGenerics();
                    break;

                case Scenario.SimpleWallTime:
                    StopSimpleWallTime();
                    break;

                case Scenario.PiComputation:
                    StopPiComputation();
                    break;

                case Scenario.FibonacciComputation:
                    StopFibonacciComputation();
                    break;

                case Scenario.Sleep:
                    StopSleep();
                    break;
            }
        }

        public void Run(Scenario scenario, int iterations, int nbThreads = 1)
        {
            Console.WriteLine($"Running {iterations} iterations of {scenario.ToString()} on {nbThreads} thread(s)");
            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < iterations; i++)
            {
                switch (scenario)
                {
                    case Scenario.All:
                        RunComputer();
                        RunGenerics();
                        RunSimpleWallTime();
                        RunPiComputation();
                        RunFibonacciComputation(nbThreads);
                        break;

                    case Scenario.Computer:
                        RunComputer();
                        break;

                    case Scenario.Generics:
                        RunGenerics();
                        break;

                    case Scenario.SimpleWallTime:
                        RunSimpleWallTime();
                        break;

                    case Scenario.PiComputation:
                        RunPiComputation();
                        break;

                    case Scenario.FibonacciComputation:
                        RunFibonacciComputation(nbThreads);
                        break;

                    case Scenario.Sleep:
                        RunSleep(nbThreads);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scenario), $"Unsupported scenario #{_scenario}");
                }
            }

            Console.WriteLine($"End of {iterations} iterations of {scenario.ToString()} in {sw.Elapsed}");
        }

        public void RunAsService(TimeSpan timeout, Scenario scenario)
        {
            var windowsService = new WindowsService(this, timeout, scenario);
            ServiceBase.Run(windowsService);
        }

        private void StartComputer()
        {
            // Check case when a generic parameter is a reference type or not
            // _computer = new Computer<byte, KeyValuePair<char, KeyValuePair<int, KeyValuePair<Single, double>>>>();
            _computer = new Computer<byte, KeyValuePair<char, KeyValuePair<int, KeyValuePair<float, object>>>>();
            _computerTask = Task.Run(_computer.Start<float, MySpecialClassA>);
        }

        private void StartGenerics()
        {
            _genericTypes = new GenericTypes();
            _genericTypes.Start();
        }

        private void StartSimpleWallTime()
        {
            _simpleWallTime = new SimpleWallTime();
            _simpleWallTime.Start();
        }

        private void StartPiComputation()
        {
            _piComputation = new PiComputation();
            _piComputation.Start();
        }

        private void StartFibonacciComputation(int nbThreads)
        {
            _fibonacciComputation = new FibonacciComputation(nbThreads);
            _fibonacciComputation.Start();
        }

        private void StartSleep(int nbThreads)
        {
            _sleepManager = new SleepManager(nbThreads);
            _sleepManager.Start();
        }

        private void StopComputer()
        {
            using (_computer)
            {
                if (!_computerTask.IsCompleted)
                {
                    Console.WriteLine($"{Environment.NewLine} ########### Stopping.");

                    _computer.Stop();
                    _computerTask.Wait();

                    Console.WriteLine($"{Environment.NewLine} ########### Stopped.");
                }

                // Force any potential exceptions embedded in the Task:
                _computerTask.GetAwaiter().GetResult();
            }
        }

        private void StopGenerics()
        {
            _genericTypes.Stop();
        }

        private void StopSimpleWallTime()
        {
            _simpleWallTime.Stop();
        }

        private void StopPiComputation()
        {
            _piComputation.Stop();
        }

        private void StopFibonacciComputation()
        {
            _fibonacciComputation.Stop();
        }

        private void StopSleep()
        {
            _sleepManager.Stop();
        }

        private void RunComputer()
        {
            using (var computer = new Computer<byte, KeyValuePair<char, KeyValuePair<int, KeyValuePair<float, object>>>>())
            {
                computer.Run<float, MySpecialClassA>();
            }
        }

        private void RunGenerics()
        {
            var gt = new GenericTypes();
            gt.Run();
        }

        private void RunSimpleWallTime()
        {
            var swt = new SimpleWallTime();
            swt.Run();
        }

        private void RunPiComputation()
        {
            var pic = new PiComputation();
            pic.Run();
        }

        private void RunFibonacciComputation(int nbThreads)
        {
            var fibo = new FibonacciComputation(nbThreads);
            fibo.Run();
        }

        private void RunSleep(int nbThreads)
        {
            var manager = new SleepManager(nbThreads);
            manager.Run();
        }

        public class MySpecialClassA
        {
        }

        public class MySpecialClassB
        {
        }

        private class WindowsService : ServiceBase
        {
            private ComputerService _computerService;
            private Scenario _scenario;

            public WindowsService(ComputerService service, TimeSpan timeout, Scenario scenario)
            {
                _computerService = service;
                _scenario = scenario;
                Task.Delay(timeout).ContinueWith(t => Stop());
            }

            protected override void OnStart(string[] args)
            {
                _computerService.StartService(_scenario, nbThreads: 1);
                base.OnStart(args);
            }

            protected override void OnStop()
            {
                _computerService.StopService();
                base.OnStop();
            }
        }
    }
#pragma warning restore CA1416
}
