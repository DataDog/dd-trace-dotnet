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
        [Fact(Skip = "This test fails when we disable parallelization for some reason")]
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

            /*****
             * WARNING: This number is expected to change if you add
             * a another test to the ducktype assembly.
             */
            if (!TestOptimization.Instance.IsRunning)
            {
#if NETFRAMEWORK
                asmDuckTypes.Should().Be(1137);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().Be(1140);
#else
                asmDuckTypes.Should().Be(1141);
#endif
            }
            else
            {
                // When running inside CI Visibility, we will generate additional duck types
#if NETFRAMEWORK
                asmDuckTypes.Should().BeGreaterThan(1137);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().BeGreaterThan(1140);
#else
                asmDuckTypes.Should().BeGreaterThan(1141);
#endif
            }
        }
    }
}
