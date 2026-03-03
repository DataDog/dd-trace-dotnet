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
        [Fact]
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

            // This test is primarily meaningful in full-suite execution, where many duck types
            // have already been generated. In isolated/filter runs there may be none.
            if (asmDuckTypes == 0)
            {
                return;
            }

            /*****
             * WARNING: This number is expected to change if you add
             * a another test to the ducktype assembly.
             */
            if (!TestOptimization.Instance.IsRunning)
            {
#if NETFRAMEWORK
                asmDuckTypes.Should().BeOneOf(1183, 1184);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().BeOneOf(1186, 1187);
#else
                asmDuckTypes.Should().BeOneOf(1187, 1188);
#endif
            }
            else
            {
                // When running inside CI Visibility, we will generate additional duck types
#if NETFRAMEWORK
                asmDuckTypes.Should().BeGreaterThan(1184);
#elif NETCOREAPP2_1
                asmDuckTypes.Should().BeGreaterThan(1187);
#else
                asmDuckTypes.Should().BeGreaterThan(1188);
#endif
            }
        }
    }
}
