// <copyright file="EnumExtensionsGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.SourceGenerators.EnumExtensions;
using Datadog.Trace.SourceGenerators.EnumExtensions.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class EnumExtensionsGeneratorTests
{
    [Fact]
    public void CanGenerateEnumExtensionsInChildNamespace()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;

            namespace MyTestNameSpace;

            [EnumExtensions]
            public enum MyEnum
            {
                First,
                Second,
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTestNameSpace;

            /// <summary>
            /// Extension methods for <see cref="MyTestNameSpace.MyEnum" />
            /// </summary>
            internal static partial class MyEnumExtensions
            {
                /// <summary>
                /// The number of members in the enum.
                /// This is a non-distinct count of defined names.
                /// </summary>
                public const int Length = 2;

                /// <summary>
                /// Returns the string representation of the <see cref="MyTestNameSpace.MyEnum"/> value.
                /// If the attribute is decorated with a <c>[Description]</c> attribute, then
                /// uses the provided value. Otherwise uses the name of the member, equivalent to
                /// calling <c>ToString()</c> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value to retrieve the string value for</param>
                /// <returns>The string representation of the value</returns>
                public static string ToStringFast(this MyTestNameSpace.MyEnum value)
                    => value switch
                    {
                        MyTestNameSpace.MyEnum.First => nameof(MyTestNameSpace.MyEnum.First),
                        MyTestNameSpace.MyEnum.Second => nameof(MyTestNameSpace.MyEnum.Second),
                        _ => value.ToString(),
                    };

                /// <summary>
                /// Retrieves an array of the values of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// </summary>
                /// <returns>An array of the values defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static MyTestNameSpace.MyEnum[] GetValues()
                    => new []
                    {
                        MyTestNameSpace.MyEnum.First,
                        MyTestNameSpace.MyEnum.Second,
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Ignores <c>[Description]</c> definitions.
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static string[] GetNames()
                    => new []
                    {
                        nameof(MyTestNameSpace.MyEnum.First),
                        nameof(MyTestNameSpace.MyEnum.Second),
                    };
            }
            """;
        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Fact]
    public void CanGenerateEnumExtensionsInNestedClass()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;

            namespace MyTestNameSpace;

            public class InnerClass
            {
                [EnumExtensions]
                public enum MyEnum
                {
                    First,
                    Second,
                }
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTestNameSpace;

            /// <summary>
            /// Extension methods for <see cref="MyTestNameSpace.InnerClass.MyEnum" />
            /// </summary>
            internal static partial class MyEnumExtensions
            {
                /// <summary>
                /// The number of members in the enum.
                /// This is a non-distinct count of defined names.
                /// </summary>
                public const int Length = 2;

                /// <summary>
                /// Returns the string representation of the <see cref="MyTestNameSpace.InnerClass.MyEnum"/> value.
                /// If the attribute is decorated with a <c>[Description]</c> attribute, then
                /// uses the provided value. Otherwise uses the name of the member, equivalent to
                /// calling <c>ToString()</c> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value to retrieve the string value for</param>
                /// <returns>The string representation of the value</returns>
                public static string ToStringFast(this MyTestNameSpace.InnerClass.MyEnum value)
                    => value switch
                    {
                        MyTestNameSpace.InnerClass.MyEnum.First => nameof(MyTestNameSpace.InnerClass.MyEnum.First),
                        MyTestNameSpace.InnerClass.MyEnum.Second => nameof(MyTestNameSpace.InnerClass.MyEnum.Second),
                        _ => value.ToString(),
                    };

                /// <summary>
                /// Retrieves an array of the values of the members defined in
                /// <see cref="MyTestNameSpace.InnerClass.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// </summary>
                /// <returns>An array of the values defined in <see cref="MyTestNameSpace.InnerClass.MyEnum" /></returns>
                public static MyTestNameSpace.InnerClass.MyEnum[] GetValues()
                    => new []
                    {
                        MyTestNameSpace.InnerClass.MyEnum.First,
                        MyTestNameSpace.InnerClass.MyEnum.Second,
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.InnerClass.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Ignores <c>[Description]</c> definitions.
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.InnerClass.MyEnum" /></returns>
                public static string[] GetNames()
                    => new []
                    {
                        nameof(MyTestNameSpace.InnerClass.MyEnum.First),
                        nameof(MyTestNameSpace.InnerClass.MyEnum.Second),
                    };
            }
            """;
        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Fact]
    public void CanGenerateEnumExtensionsWithDescription()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;
            using System.ComponentModel;

            namespace MyTestNameSpace;

            [EnumExtensions]
            public enum MyEnum
            {
                First = 0,

                [Description("2nd")]
                Second = 1,
                Third = 2,

                [Description("4th")]
                Fourth
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTestNameSpace;

            /// <summary>
            /// Extension methods for <see cref="MyTestNameSpace.MyEnum" />
            /// </summary>
            internal static partial class MyEnumExtensions
            {
                /// <summary>
                /// The number of members in the enum.
                /// This is a non-distinct count of defined names.
                /// </summary>
                public const int Length = 4;

                /// <summary>
                /// Returns the string representation of the <see cref="MyTestNameSpace.MyEnum"/> value.
                /// If the attribute is decorated with a <c>[Description]</c> attribute, then
                /// uses the provided value. Otherwise uses the name of the member, equivalent to
                /// calling <c>ToString()</c> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value to retrieve the string value for</param>
                /// <returns>The string representation of the value</returns>
                public static string ToStringFast(this MyTestNameSpace.MyEnum value)
                    => value switch
                    {
                        MyTestNameSpace.MyEnum.First => nameof(MyTestNameSpace.MyEnum.First),
                        MyTestNameSpace.MyEnum.Second => "2nd",
                        MyTestNameSpace.MyEnum.Third => nameof(MyTestNameSpace.MyEnum.Third),
                        MyTestNameSpace.MyEnum.Fourth => "4th",
                        _ => value.ToString(),
                    };

                /// <summary>
                /// Retrieves an array of the values of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// </summary>
                /// <returns>An array of the values defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static MyTestNameSpace.MyEnum[] GetValues()
                    => new []
                    {
                        MyTestNameSpace.MyEnum.First,
                        MyTestNameSpace.MyEnum.Second,
                        MyTestNameSpace.MyEnum.Third,
                        MyTestNameSpace.MyEnum.Fourth,
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Ignores <c>[Description]</c> definitions.
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static string[] GetNames()
                    => new []
                    {
                        nameof(MyTestNameSpace.MyEnum.First),
                        nameof(MyTestNameSpace.MyEnum.Second),
                        nameof(MyTestNameSpace.MyEnum.Third),
                        nameof(MyTestNameSpace.MyEnum.Fourth),
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Uses <c>[Description]</c> definition if available, otherwise uses the name of the property
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static string[] GetDescriptions()
                    => new []
                    {
                        nameof(MyTestNameSpace.MyEnum.First),
                        "2nd",
                        nameof(MyTestNameSpace.MyEnum.Third),
                        "4th",
                    };
            }
            """;
        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Fact]
    public void CanGenerateEnumExtensionsWithDuplicateDescription()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;
            using System.ComponentModel;

            namespace MyTestNameSpace;

            [EnumExtensions]
            public enum MyEnum
            {
                First = 0,

                [Description("2nd")]
                Second = 1,
                Third = 2,

                [Description("2nd")]
                Fourth
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTestNameSpace;

            /// <summary>
            /// Extension methods for <see cref="MyTestNameSpace.MyEnum" />
            /// </summary>
            internal static partial class MyEnumExtensions
            {
                /// <summary>
                /// The number of members in the enum.
                /// This is a non-distinct count of defined names.
                /// </summary>
                public const int Length = 4;

                /// <summary>
                /// Returns the string representation of the <see cref="MyTestNameSpace.MyEnum"/> value.
                /// If the attribute is decorated with a <c>[Description]</c> attribute, then
                /// uses the provided value. Otherwise uses the name of the member, equivalent to
                /// calling <c>ToString()</c> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value to retrieve the string value for</param>
                /// <returns>The string representation of the value</returns>
                public static string ToStringFast(this MyTestNameSpace.MyEnum value)
                    => value switch
                    {
                        MyTestNameSpace.MyEnum.First => nameof(MyTestNameSpace.MyEnum.First),
                        MyTestNameSpace.MyEnum.Second => "2nd",
                        MyTestNameSpace.MyEnum.Third => nameof(MyTestNameSpace.MyEnum.Third),
                        MyTestNameSpace.MyEnum.Fourth => "2nd",
                        _ => value.ToString(),
                    };

                /// <summary>
                /// Retrieves an array of the values of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// </summary>
                /// <returns>An array of the values defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static MyTestNameSpace.MyEnum[] GetValues()
                    => new []
                    {
                        MyTestNameSpace.MyEnum.First,
                        MyTestNameSpace.MyEnum.Second,
                        MyTestNameSpace.MyEnum.Third,
                        MyTestNameSpace.MyEnum.Fourth,
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Ignores <c>[Description]</c> definitions.
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static string[] GetNames()
                    => new []
                    {
                        nameof(MyTestNameSpace.MyEnum.First),
                        nameof(MyTestNameSpace.MyEnum.Second),
                        nameof(MyTestNameSpace.MyEnum.Third),
                        nameof(MyTestNameSpace.MyEnum.Fourth),
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="MyTestNameSpace.MyEnum" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Uses <c>[Description]</c> definition if available, otherwise uses the name of the property
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="MyTestNameSpace.MyEnum" /></returns>
                public static string[] GetDescriptions()
                    => new []
                    {
                        nameof(MyTestNameSpace.MyEnum.First),
                        "2nd",
                        nameof(MyTestNameSpace.MyEnum.Third),
                        "2nd",
                    };
            }
            """;
        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        Assert.Contains(diagnostics, diag => diag.Id == DuplicateDescriptionDiagnostic.Id);
        output.Should().Be(expected);
    }

    [Fact]
    public void CanGenerateIntegrationNameToKeysForIntegrationId()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;

            namespace Datadog.Trace.Configuration;

            [EnumExtensions]
            internal enum IntegrationId
            {
                HttpMessageHandler,
                AspNetCore,
                AdoNet,
            }
            """;

        const string expected = Constants.FileHeader + """

            namespace Datadog.Trace.Configuration
            {
                /// <summary>
                /// Generated mapping of integration names to their configuration keys.
                /// </summary>
                internal static partial class IntegrationNameToKeys
                {
                    private const string ObsoleteMessage = DeprecationMessages.AppAnalytics;

                    /// <summary>
                    /// All integration enabled keys (canonical + aliases).
                    /// </summary>
                    public static readonly string[] AllIntegrationEnabledKeys = new[]
                    {
                        "DD_TRACE_HTTPMESSAGEHANDLER_ENABLED", "DD_TRACE_HttpMessageHandler_ENABLED", "DD_HttpMessageHandler_ENABLED",
                        "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_ENABLED", "DD_TRACE_HttpMessageHandler_ANALYTICS_ENABLED", "DD_HttpMessageHandler_ANALYTICS_ENABLED",
                        "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_SAMPLE_RATE", "DD_TRACE_HttpMessageHandler_ANALYTICS_SAMPLE_RATE", "DD_HttpMessageHandler_ANALYTICS_SAMPLE_RATE", 
                        "DD_TRACE_ASPNETCORE_ENABLED", "DD_TRACE_AspNetCore_ENABLED", "DD_AspNetCore_ENABLED",
                        "DD_TRACE_ASPNETCORE_ANALYTICS_ENABLED", "DD_TRACE_AspNetCore_ANALYTICS_ENABLED", "DD_AspNetCore_ANALYTICS_ENABLED",
                        "DD_TRACE_ASPNETCORE_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AspNetCore_ANALYTICS_SAMPLE_RATE", "DD_AspNetCore_ANALYTICS_SAMPLE_RATE", 
                        "DD_TRACE_ADONET_ENABLED", "DD_TRACE_AdoNet_ENABLED", "DD_AdoNet_ENABLED",
                        "DD_TRACE_ADONET_ANALYTICS_ENABLED", "DD_TRACE_AdoNet_ANALYTICS_ENABLED", "DD_AdoNet_ANALYTICS_ENABLED",
                        "DD_TRACE_ADONET_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AdoNet_ANALYTICS_SAMPLE_RATE", "DD_AdoNet_ANALYTICS_SAMPLE_RATE", 
                    };
                    /// <summary>
                    /// Gets the configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    public static string[] GetIntegrationEnabledKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "HttpMessageHandler" => new[] { "DD_TRACE_HTTPMESSAGEHANDLER_ENABLED", "DD_TRACE_HttpMessageHandler_ENABLED", "DD_HttpMessageHandler_ENABLED" },
                            "AspNetCore" => new[] { "DD_TRACE_ASPNETCORE_ENABLED", "DD_TRACE_AspNetCore_ENABLED", "DD_AspNetCore_ENABLED" },
                            "AdoNet" => new[] { "DD_TRACE_ADONET_ENABLED", "DD_TRACE_AdoNet_ENABLED", "DD_AdoNet_ENABLED" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ENABLED", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ENABLED", integrationName),
                                $"DD_{integrationName}_ENABLED"
                            }
                        };
                    }
                    /// <summary>
                    /// Gets the analytics enabled configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static string[] GetIntegrationAnalyticsEnabledKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "HttpMessageHandler" => new[] { "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_ENABLED", "DD_TRACE_HttpMessageHandler_ANALYTICS_ENABLED", "DD_HttpMessageHandler_ANALYTICS_ENABLED" },
                            "AspNetCore" => new[] { "DD_TRACE_ASPNETCORE_ANALYTICS_ENABLED", "DD_TRACE_AspNetCore_ANALYTICS_ENABLED", "DD_AspNetCore_ANALYTICS_ENABLED" },
                            "AdoNet" => new[] { "DD_TRACE_ADONET_ANALYTICS_ENABLED", "DD_TRACE_AdoNet_ANALYTICS_ENABLED", "DD_AdoNet_ANALYTICS_ENABLED" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName),
                                $"DD_{integrationName}_ANALYTICS_ENABLED"
                            }
                        };
                    }
                    /// <summary>
                    /// Gets the analytics sample rate configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static string[] GetIntegrationAnalyticsSampleRateKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "HttpMessageHandler" => new[] { "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_SAMPLE_RATE", "DD_TRACE_HttpMessageHandler_ANALYTICS_SAMPLE_RATE", "DD_HttpMessageHandler_ANALYTICS_SAMPLE_RATE" },
                            "AspNetCore" => new[] { "DD_TRACE_ASPNETCORE_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AspNetCore_ANALYTICS_SAMPLE_RATE", "DD_AspNetCore_ANALYTICS_SAMPLE_RATE" },
                            "AdoNet" => new[] { "DD_TRACE_ADONET_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AdoNet_ANALYTICS_SAMPLE_RATE", "DD_AdoNet_ANALYTICS_SAMPLE_RATE" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName),
                                $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE"
                            }
                        };
                    }
                }
            }
            
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().HaveCount(3);

        var integrationNameToKeys = output.FirstOrDefault(x => x.Contains("IntegrationNameToKeys"));
        integrationNameToKeys.Should().NotBeNull();
        integrationNameToKeys.Should().Be(expected);
    }

    [Fact]
    public void DoesNotGenerateIntegrationNameToKeysForNonIntegrationIdEnum()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;

            namespace MyTestNameSpace;

            [EnumExtensions]
            public enum MyEnum
            {
                First,
                Second,
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().HaveCount(2);
        output.Should().Contain(x => x.Contains("MyEnumExtensions"));
        output.Should().NotContain(x => x.Contains("IntegrationNameToKeys"));
    }

    [Fact]
    public void IntegrationNameToKeysGeneratesCorrectKeyFormats()
    {
        const string input = """
            using Datadog.Trace.SourceGenerators;

            namespace Datadog.Trace.Configuration;

            [EnumExtensions]
            internal enum IntegrationId
            {
                MySql,
                AwsS3,
                RabbitMQ,
            }
            """;

        const string expected = Constants.FileHeader + """

            namespace Datadog.Trace.Configuration
            {
                /// <summary>
                /// Generated mapping of integration names to their configuration keys.
                /// </summary>
                internal static partial class IntegrationNameToKeys
                {
                    private const string ObsoleteMessage = DeprecationMessages.AppAnalytics;

                    /// <summary>
                    /// All integration enabled keys (canonical + aliases).
                    /// </summary>
                    public static readonly string[] AllIntegrationEnabledKeys = new[]
                    {
                        "DD_TRACE_MYSQL_ENABLED", "DD_TRACE_MySql_ENABLED", "DD_MySql_ENABLED",
                        "DD_TRACE_MYSQL_ANALYTICS_ENABLED", "DD_TRACE_MySql_ANALYTICS_ENABLED", "DD_MySql_ANALYTICS_ENABLED",
                        "DD_TRACE_MYSQL_ANALYTICS_SAMPLE_RATE", "DD_TRACE_MySql_ANALYTICS_SAMPLE_RATE", "DD_MySql_ANALYTICS_SAMPLE_RATE", 
                        "DD_TRACE_AWSS3_ENABLED", "DD_TRACE_AwsS3_ENABLED", "DD_AwsS3_ENABLED",
                        "DD_TRACE_AWSS3_ANALYTICS_ENABLED", "DD_TRACE_AwsS3_ANALYTICS_ENABLED", "DD_AwsS3_ANALYTICS_ENABLED",
                        "DD_TRACE_AWSS3_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AwsS3_ANALYTICS_SAMPLE_RATE", "DD_AwsS3_ANALYTICS_SAMPLE_RATE", 
                        "DD_TRACE_RABBITMQ_ENABLED", "DD_TRACE_RabbitMQ_ENABLED", "DD_RabbitMQ_ENABLED",
                        "DD_TRACE_RABBITMQ_ANALYTICS_ENABLED", "DD_TRACE_RabbitMQ_ANALYTICS_ENABLED", "DD_RabbitMQ_ANALYTICS_ENABLED",
                        "DD_TRACE_RABBITMQ_ANALYTICS_SAMPLE_RATE", "DD_TRACE_RabbitMQ_ANALYTICS_SAMPLE_RATE", "DD_RabbitMQ_ANALYTICS_SAMPLE_RATE", 
                    };
                    /// <summary>
                    /// Gets the configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    public static string[] GetIntegrationEnabledKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "MySql" => new[] { "DD_TRACE_MYSQL_ENABLED", "DD_TRACE_MySql_ENABLED", "DD_MySql_ENABLED" },
                            "AwsS3" => new[] { "DD_TRACE_AWSS3_ENABLED", "DD_TRACE_AwsS3_ENABLED", "DD_AwsS3_ENABLED" },
                            "RabbitMQ" => new[] { "DD_TRACE_RABBITMQ_ENABLED", "DD_TRACE_RabbitMQ_ENABLED", "DD_RabbitMQ_ENABLED" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ENABLED", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ENABLED", integrationName),
                                $"DD_{integrationName}_ENABLED"
                            }
                        };
                    }
                    /// <summary>
                    /// Gets the analytics enabled configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static string[] GetIntegrationAnalyticsEnabledKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "MySql" => new[] { "DD_TRACE_MYSQL_ANALYTICS_ENABLED", "DD_TRACE_MySql_ANALYTICS_ENABLED", "DD_MySql_ANALYTICS_ENABLED" },
                            "AwsS3" => new[] { "DD_TRACE_AWSS3_ANALYTICS_ENABLED", "DD_TRACE_AwsS3_ANALYTICS_ENABLED", "DD_AwsS3_ANALYTICS_ENABLED" },
                            "RabbitMQ" => new[] { "DD_TRACE_RABBITMQ_ANALYTICS_ENABLED", "DD_TRACE_RabbitMQ_ANALYTICS_ENABLED", "DD_RabbitMQ_ANALYTICS_ENABLED" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName),
                                $"DD_{integrationName}_ANALYTICS_ENABLED"
                            }
                        };
                    }
                    /// <summary>
                    /// Gets the analytics sample rate configuration keys for the specified integration name.
                    /// Returns keys in order: [canonical key, alias1, alias2]
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>Array of configuration keys to check in order</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static string[] GetIntegrationAnalyticsSampleRateKeys(string integrationName)
                    {
                        return integrationName switch
                        {
                            "MySql" => new[] { "DD_TRACE_MYSQL_ANALYTICS_SAMPLE_RATE", "DD_TRACE_MySql_ANALYTICS_SAMPLE_RATE", "DD_MySql_ANALYTICS_SAMPLE_RATE" },
                            "AwsS3" => new[] { "DD_TRACE_AWSS3_ANALYTICS_SAMPLE_RATE", "DD_TRACE_AwsS3_ANALYTICS_SAMPLE_RATE", "DD_AwsS3_ANALYTICS_SAMPLE_RATE" },
                            "RabbitMQ" => new[] { "DD_TRACE_RABBITMQ_ANALYTICS_SAMPLE_RATE", "DD_TRACE_RabbitMQ_ANALYTICS_SAMPLE_RATE", "DD_RabbitMQ_ANALYTICS_SAMPLE_RATE" },
                            _ => new[]
                            {
                                string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName.ToUpperInvariant()),
                                string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName),
                                $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE"
                            }
                        };
                    }
                }
            }
            
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedTrees<EnumExtensionsGenerator>(input);

        using var s = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().HaveCount(3);

        var integrationNameToKeys = output.FirstOrDefault(x => x.Contains("IntegrationNameToKeys"));
        integrationNameToKeys.Should().NotBeNull();
        integrationNameToKeys.Should().Be(expected);
    }
}
