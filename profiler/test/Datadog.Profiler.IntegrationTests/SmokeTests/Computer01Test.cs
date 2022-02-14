// <copyright file="Computer01Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class Computer01Test
    {
        private readonly ITestOutputHelper _output;

        public Computer01Test(ITestOutputHelper output)
        {
            _output = output;
        }

        // scenarios implemented in Computer01:
        // -----------------------------------------------------------------------------------------
        //  1: start threads with specific callstacks in another appdomain
        //  2: start threads with generic type and method having long parameters list in callstack
        //  3: start threads that sleep/task.delay for 10s, 20s, 30s, 40s every minute
        //  4: start a thread to compute pi at a certain precision(high CPU usage)
        //  5: start a to compute fibonacci (high CPU usage + deep stacks)
        // -----------------------------------------------------------------------------------------
        [SmokeFact("Datadog.Demos.Computer01", DisplayName = "AppDomaim")]
        public void CheckAppDomain(string appName, string framework, string appAssembly)
        {
            new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", _output).RunAndCheck();
        }

        [SmokeFact("Datadog.Demos.Computer01", DisplayName = "Generics")]
        public void CheckGenerics(string appName, string framework, string appAssembly)
        {
            new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 2", _output).RunAndCheck();
        }

        [SmokeFact("Datadog.Demos.Computer01", DisplayName = "Pi")]
        public void CheckPi(string appName, string framework, string appAssembly)
        {
            new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 4", _output).RunAndCheck();
        }

        [SmokeFact("Datadog.Demos.Computer01", DisplayName = "Fibonacci")]
        public void CheckFibonacci(string appName, string framework, string appAssembly)
        {
            new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 5", _output).RunAndCheck();
        }
    }
}
