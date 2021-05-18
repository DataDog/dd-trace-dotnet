// <copyright file="TestRunners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public class TestRunners
    {
        public static readonly IEnumerable<string> ValidNames = new[]
                                                                {
                                                                    "testhost",
                                                                    "testhost.x86",
                                                                    "testhost.net452.x86",
                                                                    "testhost.net461.x86",
                                                                    "vstest.console",
                                                                    "xunit.console.x86",
                                                                    "xunit.console.x64",
                                                                    "ReSharperTestRunner64",
                                                                    "ReSharperTestRunner64c"
                                                                };
    }
}
