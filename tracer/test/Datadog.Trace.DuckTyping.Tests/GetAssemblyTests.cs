// <copyright file="GetAssemblyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    [Collection(nameof(GetAssemblyTestsCollection))]
    public class GetAssemblyTests
    {
        [Fact]
        public void GetAssemblyTest()
        {
            var lstExceptions = new List<Exception>();
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var duckTypeAssemblies = new List<Assembly>();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeGenericTypeAssemblyPrefix) ||
                    assembly.FullName!.StartsWith(DuckTypeConstants.DuckTypeNotVisibleAssemblyPrefix))
                {
                    duckTypeAssemblies.Add(assembly);

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

            bool isUsingCiApp = Environment.GetEnvironmentVariable("DDCIAPP_CIVISIBILITY_ENABLED") is { Length: > 0 } envVar
                             && (envVar == "1" || (bool.TryParse(envVar, out var isEnabled) && isEnabled));
            /*****
             * WARNING: This number is expected to change if you add
             * a another test to the ducktype assembly.
             */
            // when we're instrumenting CIapp we run into this issue
#if NETFRAMEWORK
            var expectedCount = isUsingCiApp ? 1143 : 1131;
#elif NETCOREAPP2_1
            var expectedCount = isUsingCiApp ? 1148 : 1134;
#else
            var expectedCount = isUsingCiApp ? 1149 : 1135;
#endif
            duckTypeAssemblies.Should().HaveCount(expectedCount);
        }
    }
}
