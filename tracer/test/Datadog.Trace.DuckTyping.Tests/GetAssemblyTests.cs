// <copyright file="GetAssemblyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
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

            /*****
             * WARNING: This number is expected to change if you add
             * a another test to the ducktype assembly.
             */
#if NETFRAMEWORK
            Assert.Equal(1131, asmDuckTypes);
#elif NETCOREAPP2_1
            Assert.Equal(1134, asmDuckTypes);
#else
            Assert.Equal(1135, asmDuckTypes);
#endif
        }
    }
}
