// <copyright file="ComputerService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Samples.Computer01
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
        private AsyncComputation _asyncComputation;
        private IteratorComputation _iteratorComputation;
        private GenericsAllocation _genericsAllocation;
        private ContentionGenerator _contentionGenerator;
        private GarbageCollections _garbageCollections;
        private MemoryLeak _memoryLeak;
        private QuicklyDeadThreads _quicklyDeadThreads;

#if NET6_0_OR_GREATER
        private LinuxSignalHandler _linuxSignalHandler;
#endif
        private LinuxMallocDeadLock _linuxMallockDeadlock;
        private MeasureAllocations _measureAllocations;
        private InnerMethods _innerMethods;
        private LineNumber _lineNumber;
        private NullThreadNameBugCheck _nullThreadNameBugCheck;
        private MethodsSignature _methodsSignature;
        private SigSegvHandlerExecution _sigsegvHandler;
#if NETCOREAPP3_0_OR_GREATER
        private LinuxDlIteratePhdrDeadlock _linuxDlIteratePhdrDeadlock;
#endif

#if NET5_0_OR_GREATER
        private OpenLdapCrash _openldapCrash;
        private SocketTimeout _socketTest;
#endif
        private Obfuscation _obfuscation;
        private ThreadSpikes _threadSpikes;
        private StringConcat _stringConcat;

        public void StartService(Scenario scenario, int nbThreads, int parameter)
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
                    StartAsyncComputation(nbThreads);
                    StartIteratorComputation(nbThreads);
                    StartGenericsAllocation(nbThreads);
                    StartContentionGenerator(nbThreads, parameter);
                    StartLineNumber();
                    StartMethodsSignature();
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

                case Scenario.Async:
                    StartAsyncComputation(nbThreads);
                    break;

                case Scenario.Iterator:
                    StartIteratorComputation(nbThreads);
                    break;

                case Scenario.GenericsAllocation:
                    StartGenericsAllocation(nbThreads);
                    break;

                case Scenario.ContentionGenerator:
                    StartContentionGenerator(nbThreads, parameter);
                    break;

#if NET6_0_OR_GREATER
                case Scenario.LinuxSignalHandler:
                    StartLinuxSignalHandler();
                    break;
#endif
                case Scenario.GarbageCollection:
                    StartGarbageCollections(parameter);
                    break;

                case Scenario.MemoryLeak:
                    StartMemoryLeak(parameter);
                    break;

                case Scenario.QuicklyDeadThreads:
                    StartQuicklyDeadThreads(nbThreads, parameter);
                    break;

                case Scenario.LinuxMallocDeadlock:
                    StartLinuxMallocDeadlock();
                    break;

                case Scenario.MeasureAllocations:
                    StartMeasureAllocations();
                    break;

                case Scenario.InnerMethods:
                    StartInnerMethods();
                    break;

                case Scenario.LineNumber:
                    StartLineNumber();
                    break;

                case Scenario.NullThreadNameBug:
                    StartNullThreadNameBugCheck();
                    break;

                case Scenario.MethodSignature:
                    StartMethodsSignature();
                    break;

#if NET5_0_OR_GREATER
                case Scenario.OpenLdapCrash:
                    StartOpenLdapCrash();
                    break;
                case Scenario.SocketTimeout:
                    StartSocketTimeout();
                    break;
#endif
                case Scenario.Obfuscation:
                    StartObfuscation();
                    break;

                case Scenario.ForceSigSegvHandler:
                    StartForceSigSegvHandler();
                    break;

                case Scenario.ThreadSpikes:
                    StartThreadSpikes(nbThreads, parameter);
                    break;

                case Scenario.StringConcat:
                    StartStringConcat(parameter);
                    break;

#if NETCOREAPP3_0_OR_GREATER
                case Scenario.LinuxDlIteratePhdrDeadlock:
                    StartLinuxDlIteratePhdrDeadlock();
                    break;
#endif

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
                    StopAsyncComputation();
                    StopIteratorComputation();
                    StopGenericsAllocation();
                    StopContentionGenerator();
                    StopLineNumber();
                    StopMethodsSignature();
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

                case Scenario.Async:
                    StopAsyncComputation();
                    break;

                case Scenario.Iterator:
                    StopIteratorComputation();
                    break;

                case Scenario.GenericsAllocation:
                    StopGenericsAllocation();
                    break;

                case Scenario.ContentionGenerator:
                    StopContentionGenerator();
                    break;

#if NET6_0_OR_GREATER
                case Scenario.LinuxSignalHandler:
                    StopLinuxSignalHandler();
                    break;
#endif

                case Scenario.GarbageCollection:
                    StopGarbageCollections();
                    break;

                case Scenario.MemoryLeak:
                    StopMemoryLeak();
                    break;

                case Scenario.QuicklyDeadThreads:
                    StopQuicklyDeadThreads();
                    break;

                case Scenario.LinuxMallocDeadlock:
                    StopLinuxMallocDeadlock();
                    break;

                case Scenario.MeasureAllocations:
                    StopMeasureAllocations();
                    break;

                case Scenario.InnerMethods:
                    StopInnerMethods();
                    break;

                case Scenario.LineNumber:
                    StopLineNumber();
                    break;

                case Scenario.NullThreadNameBug:
                    StopNullThreadNameBugCheck();
                    break;

                case Scenario.MethodSignature:
                    StopMethodsSignature();
                    break;

#if NET5_0_OR_GREATER
                case Scenario.OpenLdapCrash:
                    StopOpenLdapCrash();
                    break;
                case Scenario.SocketTimeout:
                    StopSocketTimeout();
                    break;
#endif

                case Scenario.Obfuscation:
                    StopObfuscation();
                    break;

                case Scenario.ForceSigSegvHandler:
                    StopForceSigSegvHandler();
                    break;

                case Scenario.ThreadSpikes:
                    StopThreadSpikes();
                    break;

                case Scenario.StringConcat:
                    StopStringConcat();
                    break;

#if NETCOREAPP3_0_OR_GREATER
                case Scenario.LinuxDlIteratePhdrDeadlock:
                    StopLinuxDlIteratePhdrDeadlock();
                    break;
#endif
            }
        }

        public void Run(Scenario scenario, int iterations, int nbThreads, int parameter)
        {
            Console.WriteLine($"Running {iterations} iterations of {scenario.ToString()} on {nbThreads} thread(s)");
            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"___Iteration {i + 1}___");

                switch (scenario)
                {
                    case Scenario.All:
                        RunComputer();
                        RunGenerics();
                        RunSimpleWallTime();
                        RunPiComputation();
                        RunFibonacciComputation(nbThreads);
                        RunSleep(nbThreads);
                        RunAsyncComputation(nbThreads);
                        RunIteratorComputation(nbThreads);
                        RunGenericsAllocation(nbThreads);
                        RunContentionGenerator(nbThreads, parameter);
                        RunLineNumber();
                        RunMethodsSignature();
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

                    case Scenario.Async:
                        RunAsyncComputation(nbThreads);
                        break;

                    case Scenario.Iterator:
                        RunIteratorComputation(nbThreads);
                        break;

                    case Scenario.GenericsAllocation:
                        RunGenericsAllocation(nbThreads);
                        break;

                    case Scenario.ContentionGenerator:
                        RunContentionGenerator(nbThreads, parameter);
                        break;

#if NET6_0_OR_GREATER
                    case Scenario.LinuxSignalHandler:
                        RunLinuxSignalHandler();
                        break;
#endif
                    case Scenario.GarbageCollection:
                        RunGarbageCollections(parameter);
                        break;

                    case Scenario.MemoryLeak:
                        RunMemoryLeak(parameter);
                        break;

                    case Scenario.QuicklyDeadThreads:
                        RunQuicklyDeadThreads(nbThreads, parameter);
                        break;

                    case Scenario.LinuxMallocDeadlock:
                        RunLinuxMallocDeadlock();
                        break;

                    case Scenario.MeasureAllocations:
                        RunMeasureAllocations();
                        break;

                    case Scenario.InnerMethods:
                        RunInnerMethods();
                        break;

                    case Scenario.LineNumber:
                        RunLineNumber();
                        break;

                    case Scenario.NullThreadNameBug:
                        RunNullThreadNameBugCheck();
                        break;

                    case Scenario.MethodSignature:
                        RunMethodsSignature();
                        break;

#if NET5_0_OR_GREATER
                    case Scenario.OpenLdapCrash:
                        RunOpenLdapCrash();
                        break;
                    case Scenario.SocketTimeout:
                        RunSocketTimeout();
                        break;
#endif
                    case Scenario.ForceSigSegvHandler:
                        RunForceSigSegvHandler();
                        break;

                    case Scenario.Obfuscation:
                        RunObfuscation();
                        break;

                    case Scenario.ThreadSpikes:
                        RunThreadSpikes(nbThreads, parameter);
                        break;

                    case Scenario.StringConcat:
                        RunStringConcat(parameter);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scenario), $"Unsupported scenario #{_scenario}");
                }

                Console.WriteLine();
            }

            Console.WriteLine($"End of {iterations} iterations of {scenario.ToString()} in {sw.Elapsed}");
        }

        public void RunAsService(TimeSpan timeout, Scenario scenario, int parameter)
        {
            var windowsService = new WindowsService(this, timeout, scenario, parameter);
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

        private void StartAsyncComputation(int nbThreads)
        {
            _asyncComputation = new AsyncComputation(nbThreads);
            _asyncComputation.Start();
        }

        private void StartIteratorComputation(int nbThreads)
        {
            _iteratorComputation = new IteratorComputation(nbThreads);
            _iteratorComputation.Start();
        }

        private void StartGenericsAllocation(int nbThreads)
        {
            _genericsAllocation = new GenericsAllocation(nbThreads);
            _genericsAllocation.Start();
        }

        private void StartContentionGenerator(int nbThreads, int parameter)
        {
            if (parameter == int.MaxValue)
            {
                // 300 ms contention by default
                parameter = 300;
            }

            _contentionGenerator = new ContentionGenerator(nbThreads, parameter);
            _contentionGenerator.Start();
        }

#if NET6_0_OR_GREATER
        private void StartLinuxSignalHandler()
        {
            _linuxSignalHandler = new LinuxSignalHandler();
            _linuxSignalHandler.Start();
        }
#endif
        private void StartGarbageCollections(int parameter)
        {
            if (parameter == int.MaxValue)
            {
                // gen0 GC by default
                parameter = 0;
            }

            _garbageCollections = new GarbageCollections(parameter);
            _garbageCollections.Start();
        }

        private void StartMemoryLeak(int parameter)
        {
            // by default, no limit to allocations

            _memoryLeak = new MemoryLeak(parameter);
            _memoryLeak.Start();
        }

        private void StartQuicklyDeadThreads(int nbThreads, int nbThreadsToCreate)
        {
            if (nbThreadsToCreate == int.MaxValue)
            {
                // 1024 threads by default
                nbThreadsToCreate = 1024;
            }

            _quicklyDeadThreads = new QuicklyDeadThreads(nbThreads, nbThreadsToCreate);
            _quicklyDeadThreads.Start();
        }

        private void StartLinuxMallocDeadlock()
        {
            _linuxMallockDeadlock = new LinuxMallocDeadLock();
            _linuxMallockDeadlock.Start();
        }

#if NETCOREAPP3_0_OR_GREATER
        private void StartLinuxDlIteratePhdrDeadlock()
        {
            _linuxDlIteratePhdrDeadlock = new LinuxDlIteratePhdrDeadlock();
            _linuxDlIteratePhdrDeadlock.Start();
        }
#endif

        private void StartMeasureAllocations()
        {
            _measureAllocations = new MeasureAllocations();
            _measureAllocations.Start();
        }

        private void StartInnerMethods()
        {
            _innerMethods = new InnerMethods();
            _innerMethods.Start();
        }

        private void StartLineNumber()
        {
            _lineNumber = new LineNumber();
            _lineNumber.Start();
        }

        private void StartNullThreadNameBugCheck()
        {
            _nullThreadNameBugCheck = new NullThreadNameBugCheck();
            _nullThreadNameBugCheck.Start();
        }

        private void StartMethodsSignature()
        {
            _methodsSignature = new MethodsSignature();
            _methodsSignature.Start();
        }

#if NET5_0_OR_GREATER
        private void StartOpenLdapCrash()
        {
            _openldapCrash = new OpenLdapCrash();
            _openldapCrash.Start();
        }

        private void StartSocketTimeout()
        {
            _socketTest = new SocketTimeout();
            _socketTest.Start();
        }
#endif

        private void StartObfuscation()
        {
            _obfuscation = new Obfuscation();
            _obfuscation.Start();
        }

        private void StopForceSigSegvHandler()
        {
            _sigsegvHandler.Stop();
        }

        private void StartForceSigSegvHandler()
        {
            _sigsegvHandler = new SigSegvHandlerExecution();
            _sigsegvHandler.Start();
        }

        private void StartThreadSpikes(int threadCount, int duration)
        {
            _threadSpikes = new ThreadSpikes(threadCount, duration);
            _threadSpikes.Start();
        }

        private void StartStringConcat(int count)
        {
            _stringConcat = new StringConcat(count);
            _stringConcat.Start();
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

        private void StopAsyncComputation()
        {
            _asyncComputation.Stop();
        }

        private void StopIteratorComputation()
        {
            _iteratorComputation.Stop();
        }

        private void StopGenericsAllocation()
        {
            _genericsAllocation.Stop();
        }

        private void StopContentionGenerator()
        {
            _contentionGenerator.Stop();
        }

#if NET6_0_OR_GREATER
        private void StopLinuxSignalHandler()
        {
            _linuxSignalHandler.Stop();
        }

        private void StopSocketTimeout()
        {
            _socketTest.Stop();
        }
#endif

        private void StopObfuscation()
        {
            _obfuscation.Stop();
        }

        private void StopGarbageCollections()
        {
            _garbageCollections.Stop();
        }

        private void StopMemoryLeak()
        {
            _memoryLeak.Stop();
        }

        private void StopQuicklyDeadThreads()
        {
            _quicklyDeadThreads.Stop();
        }

        private void StopLinuxMallocDeadlock()
        {
            _linuxMallockDeadlock.Stop();
        }

#if NETCOREAPP3_0_OR_GREATER
        private void StopLinuxDlIteratePhdrDeadlock()
        {
            _linuxDlIteratePhdrDeadlock.Stop();
        }
#endif

        private void StopMeasureAllocations()
        {
            _measureAllocations.Stop();
        }

        private void StopInnerMethods()
        {
            _innerMethods.Stop();
        }

        private void StopLineNumber()
        {
            _lineNumber.Stop();
        }

        private void StopNullThreadNameBugCheck()
        {
            _nullThreadNameBugCheck.Stop();
        }

        private void StopMethodsSignature()
        {
            _methodsSignature.Stop();
        }

#if NET5_0_OR_GREATER
        private void StopOpenLdapCrash()
        {
            _openldapCrash.Stop();
        }
#endif

        private void StopThreadSpikes()
        {
            _threadSpikes.Stop();
        }

        private void StopStringConcat()
        {
            _stringConcat.Stop();
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

        private void RunAsyncComputation(int nbThreads)
        {
            var computation = new AsyncComputation(nbThreads);
            computation.Run();
        }

        private void RunIteratorComputation(int nbThreads)
        {
            var computation = new AsyncComputation(nbThreads);
            computation.Run();
        }

        private void RunGenericsAllocation(int nbThreads)
        {
            var allocations = new GenericsAllocation(nbThreads);
            allocations.Run();
        }

        private void RunContentionGenerator(int nbThreads, int parameter)
        {
            if (parameter == int.MaxValue)
            {
                // 300 ms contention by default
                parameter = 300;
            }

            var contentionGenerator = new ContentionGenerator(nbThreads, parameter);
            contentionGenerator.Run();
        }

#if NET6_0_OR_GREATER
        private void RunLinuxSignalHandler()
        {
            var linuxSignalHandler = new LinuxSignalHandler();
            linuxSignalHandler.Run();
        }
#endif

        private void RunGarbageCollections(int parameter)
        {
            if (parameter == int.MaxValue)
            {
                // gen0 collection by default
                parameter = 0;
            }

            var garbageCollections = new GarbageCollections(parameter);
            garbageCollections.Run();
        }

        private void RunMemoryLeak(int parameter)
        {
            var memoryLeak = new MemoryLeak(parameter);
            memoryLeak.Run();
        }

        private void RunQuicklyDeadThreads(int nbThreads, int nbThreadsToCreate)
        {
            if (nbThreadsToCreate == int.MaxValue)
            {
                // 1024 threads by default
                nbThreadsToCreate = 1024;
            }

            var quicklyDeadThreads = new QuicklyDeadThreads(nbThreads, nbThreadsToCreate);
            quicklyDeadThreads.Run();
        }

        private void RunLinuxMallocDeadlock()
        {
            var linuxSignalHandler = new LinuxMallocDeadLock();
            linuxSignalHandler.Run();
        }

        private void RunMeasureAllocations()
        {
            var measureAllocations = new MeasureAllocations();
            measureAllocations.Run();
        }

        private void RunInnerMethods()
        {
            var innerMethods = new InnerMethods();
            innerMethods.Run();
        }

        private void RunLineNumber()
        {
            var lineNumber = new LineNumber();
            lineNumber.Run();
        }

        private void RunNullThreadNameBugCheck()
        {
            var nullThreadNameBugCheck = new NullThreadNameBugCheck();
            nullThreadNameBugCheck.Run();
        }

        private void RunMethodsSignature()
        {
            var methodsSignature = new MethodsSignature();
            methodsSignature.Run();
        }

#if NET5_0_OR_GREATER
        private void RunOpenLdapCrash()
        {
            var openldapCrash = new OpenLdapCrash();
            openldapCrash.Run();
        }

        private void RunSocketTimeout()
        {
            var socketTest = new SocketTimeout();
            socketTest.Run();
        }
#endif

        private void RunForceSigSegvHandler()
        {
            var test = new SigSegvHandlerExecution();
            test.Run();
        }

        private void RunObfuscation()
        {
            var test = new Obfuscation();
            test.Run();
        }

        private void RunThreadSpikes(int threadCount, int duration)
        {
            var test = new ThreadSpikes(threadCount, duration);
            test.Run();
        }

        private void RunStringConcat(int count)
        {
            var test = new StringConcat(count);
            test.Run();
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
            private int _parameter;

            public WindowsService(ComputerService service, TimeSpan timeout, Scenario scenario, int parameter)
            {
                _computerService = service;
                _scenario = scenario;
                _parameter = parameter;
                Task.Delay(timeout).ContinueWith(t => Stop());
            }

            protected override void OnStart(string[] args)
            {
                _computerService.StartService(_scenario, nbThreads: 1, _parameter);
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
