// <copyright file="GetAssemblyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class GetAssemblyTests
    {
        private const string TestModeEnvironmentVariable = "DD_DUCKTYPE_TEST_MODE";
        private const string AotModeValue = "aot";
        private const string DynamicModeValue = "dynamic";

        [Fact]
        [Trait("SkipInCI", "True")]
        public void GetAssemblyTest()
        {
            var asmDuckTypes = 0;
            var lstExceptions = new List<Exception>();
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeGenericTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeNotVisibleAssemblyPrefix))
                {
                    asmDuckTypes++;

                    try
                    {
                        assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        lstExceptions.AddRange(ex.LoaderExceptions);
                    }
                }
            }

            if (lstExceptions.Count > 0)
            {
                throw new AggregateException(lstExceptions.ToArray());
            }

            // This test is primarily meaningful after other tests have generated ducktype assemblies.
            // In isolated/filter runs, or when it runs early in a randomized full-suite process, there may be none.
            if (asmDuckTypes == 0)
            {
                return;
            }

            // In explicit AOT/dynamic parity modes the assembly count depends on the selected mode and
            // randomized test order. The GetTypes() validation above is still useful for assemblies
            // loaded so far, but the lower-bound assertion is not stable in these modes.
            var testMode = Environment.GetEnvironmentVariable(TestModeEnvironmentVariable);
            if (string.Equals(testMode, AotModeValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(testMode, DynamicModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            /*****
             * WARNING: This number is expected to change if you add
             * another test to the ducktype assembly. Generic signature
             * validation also lowers this count when an invalid proxy is
             * rejected before its dynamic assembly is created.
             */
            if (!TestOptimization.Instance.IsRunning)
            {
#if NETFRAMEWORK
                asmDuckTypes.Should().Be(1516);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().Be(1526);
#else
                asmDuckTypes.Should().Be(1527);
#endif
            }
            else
            {
                // When running inside CI Visibility, we will generate additional duck types
#if NETFRAMEWORK
                asmDuckTypes.Should().BeGreaterThan(1516);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().BeGreaterThan(1526);
#else
                asmDuckTypes.Should().BeGreaterThan(1527);
#endif
            }
        }
    }
}
