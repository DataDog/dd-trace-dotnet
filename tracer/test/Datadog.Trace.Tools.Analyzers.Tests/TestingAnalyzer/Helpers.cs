// <copyright file="Helpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Analyzers.Tests.TestingAnalyzer;

public static class Helpers
{
    /// <summary>
    /// Stub definitions for xUnit attributes and EnvironmentRestorerAttribute.
    /// These are needed because the analyzer test compilation does not reference
    /// xUnit or Datadog.Trace.TestHelpers directly.
    /// </summary>
    public static string TypeDefinitions { get; } =
        """
        namespace Xunit
        {
            using System;

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
            public class FactAttribute : Attribute { }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
            public class TheoryAttribute : FactAttribute { }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
            public class SkippableFactAttribute : FactAttribute { }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
            public class SkippableTheoryAttribute : TheoryAttribute { }
        }

        namespace Datadog.Trace.TestHelpers
        {
            using System;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
            public class EnvironmentRestorerAttribute : Attribute
            {
                public EnvironmentRestorerAttribute(params string[] args) { }
            }
        }
        """;
}
