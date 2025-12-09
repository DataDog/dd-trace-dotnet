// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;

namespace Datadog.Trace.SourceGenerators.EnumExtensions;

internal class Sources
{
    public const string Attributes = Constants.FileHeader + """
        namespace Datadog.Trace.SourceGenerators;

        /// <summary>
        /// Add to enums to indicate that extension methods should be generated for the type
        /// </summary>
        [System.AttributeUsage(System.AttributeTargets.Enum)]
        [System.Diagnostics.Conditional("DEBUG")]
        internal class EnumExtensionsAttribute : System.Attribute
        {
        }
        """;

    public static string GenerateExtensionClass(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        return Constants.FileHeader + $$"""
            namespace {{enumToGenerate.Namespace}};

            /// <summary>
            /// Extension methods for <see cref="{{enumToGenerate.FullyQualifiedName}}" />
            /// </summary>
            internal static partial class {{enumToGenerate.ExtensionsName}}
            {
                /// <summary>
                /// The number of members in the enum.
                /// This is a non-distinct count of defined names.
                /// </summary>
                public const int Length = {{enumToGenerate.Names.Count}};

                /// <summary>
                /// Returns the string representation of the <see cref="{{enumToGenerate.FullyQualifiedName}}"/> value.
                /// If the attribute is decorated with a <c>[Description]</c> attribute, then
                /// uses the provided value. Otherwise uses the name of the member, equivalent to
                /// calling <c>ToString()</c> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value to retrieve the string value for</param>
                /// <returns>The string representation of the value</returns>
                public static string ToStringFast(this {{enumToGenerate.FullyQualifiedName}} value)
                    => value switch
                    {{{GetToStringFast(sb, in enumToGenerate)}}
                        _ => value.ToString(),
                    };{{GetHasFlags(in enumToGenerate)}}

                /// <summary>
                /// Retrieves an array of the values of the members defined in
                /// <see cref="{{enumToGenerate.FullyQualifiedName}}" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// </summary>
                /// <returns>An array of the values defined in <see cref="{{enumToGenerate.FullyQualifiedName}}" /></returns>
                public static {{enumToGenerate.FullyQualifiedName}}[] GetValues()
                    => new []
                    {{{GetValues(sb, in enumToGenerate)}}
                    };

                /// <summary>
                /// Retrieves an array of the names of the members defined in
                /// <see cref="{{enumToGenerate.FullyQualifiedName}}" />.
                /// Note that this returns a new array with every invocation, so
                /// should be cached if appropriate.
                /// Ignores <c>[Description]</c> definitions.
                /// </summary>
                /// <returns>An array of the names of the members defined in <see cref="{{enumToGenerate.FullyQualifiedName}}" /></returns>
                public static string[] GetNames()
                    => new []
                    {{{GetNames(sb, in enumToGenerate)}}
                    };{{GetDescriptions(sb, in enumToGenerate)}}
            }
            """;
    }

    public static string GenerateIntegrationNameToKeys(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        var arrayKeys = new StringBuilder();
        var switchCasesEnabled = new StringBuilder();
        var switchCasesAnalyticsEnabled = new StringBuilder();
        var switchCasesAnalyticsSampleRate = new StringBuilder();

        // Single loop to build array keys and all switch cases
        foreach (var member in enumToGenerate.Names)
        {
            var name = member.Property;
            var upperName = name.ToUpperInvariant();

            // Configuration key pattern for enabling or disabling an integration
            var upperKey = $"DD_TRACE_{upperName}_ENABLED";
            var mixedKey = $"DD_TRACE_{name}_ENABLED";
            var shortKey = $"DD_{name}_ENABLED";
            arrayKeys.AppendLine($"            \"{upperKey}\", \"{mixedKey}\", \"{shortKey}\",");
            switchCasesEnabled.AppendLine($"                \"{name}\" => new[] {{ \"{upperKey}\", \"{mixedKey}\", \"{shortKey}\" }},");

            // Configuration key pattern for enabling or disabling Analytics in an integration
            var analyticsUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_ENABLED";
            var analyticsMixedKey = $"DD_TRACE_{name}_ANALYTICS_ENABLED";
            var analyticsShortKey = $"DD_{name}_ANALYTICS_ENABLED";
            switchCasesAnalyticsEnabled.AppendLine($"                \"{name}\" => new[] {{ \"{analyticsUpperKey}\", \"{analyticsMixedKey}\", \"{analyticsShortKey}\" }},");
            arrayKeys.AppendLine($"            \"{analyticsUpperKey}\", \"{analyticsMixedKey}\", \"{analyticsShortKey}\",");

            // Configuration key pattern for setting Analytics sampling rate in an integration
            var sampleRateUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_SAMPLE_RATE";
            var sampleRateMixedKey = $"DD_TRACE_{name}_ANALYTICS_SAMPLE_RATE";
            var sampleRateShortKey = $"DD_{name}_ANALYTICS_SAMPLE_RATE";
            switchCasesAnalyticsSampleRate.AppendLine($"                \"{name}\" => new[] {{ \"{sampleRateUpperKey}\", \"{sampleRateMixedKey}\", \"{sampleRateShortKey}\" }},");
            arrayKeys.AppendLine($"            \"{sampleRateUpperKey}\", \"{sampleRateMixedKey}\", \"{sampleRateShortKey}\", ");
        }

        sb.Clear();
        sb.AppendLine(Constants.FileHeader);
        sb.AppendLine("namespace Datadog.Trace.Configuration");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generated mapping of integration names to their configuration keys.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static partial class IntegrationNameToKeys");
        sb.AppendLine("    {");
        sb.AppendLine("        private const string ObsoleteMessage = DeprecationMessages.AppAnalytics;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// All integration enabled keys (canonical + aliases).");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static readonly string[] AllIntegrationEnabledKeys = new[]");
        sb.AppendLine("        {");
        sb.Append(arrayKeys);
        sb.AppendLine("        };");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the configuration keys for the specified integration name.");
        sb.AppendLine("        /// Returns keys in order: [canonical key, alias1, alias2]");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"integrationName\">The integration name</param>");
        sb.AppendLine("        /// <returns>Array of configuration keys to check in order</returns>");
        sb.AppendLine("        public static string[] GetIntegrationEnabledKeys(string integrationName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return integrationName switch");
        sb.AppendLine("            {");
        sb.Append(switchCasesEnabled);
        sb.AppendLine("                _ => new[]");
        sb.AppendLine("                {");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ENABLED\", integrationName.ToUpperInvariant()),");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ENABLED\", integrationName),");
        sb.AppendLine("                    $\"DD_{integrationName}_ENABLED\"");
        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the analytics enabled configuration keys for the specified integration name.");
        sb.AppendLine("        /// Returns keys in order: [canonical key, alias1, alias2]");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"integrationName\">The integration name</param>");
        sb.AppendLine("        /// <returns>Array of configuration keys to check in order</returns>");
        sb.AppendLine("        [System.Obsolete(ObsoleteMessage)]");
        sb.AppendLine("        public static string[] GetIntegrationAnalyticsEnabledKeys(string integrationName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return integrationName switch");
        sb.AppendLine("            {");
        sb.Append(switchCasesAnalyticsEnabled);
        sb.AppendLine("                _ => new[]");
        sb.AppendLine("                {");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ANALYTICS_ENABLED\", integrationName.ToUpperInvariant()),");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ANALYTICS_ENABLED\", integrationName),");
        sb.AppendLine("                    $\"DD_{integrationName}_ANALYTICS_ENABLED\"");
        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the analytics sample rate configuration keys for the specified integration name.");
        sb.AppendLine("        /// Returns keys in order: [canonical key, alias1, alias2]");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"integrationName\">The integration name</param>");
        sb.AppendLine("        /// <returns>Array of configuration keys to check in order</returns>");
        sb.AppendLine("        [System.Obsolete(ObsoleteMessage)]");
        sb.AppendLine("        public static string[] GetIntegrationAnalyticsSampleRateKeys(string integrationName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return integrationName switch");
        sb.AppendLine("            {");
        sb.Append(switchCasesAnalyticsSampleRate);
        sb.AppendLine("                _ => new[]");
        sb.AppendLine("                {");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE\", integrationName.ToUpperInvariant()),");
        sb.AppendLine("                    string.Format(\"DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE\", integrationName),");
        sb.AppendLine("                    $\"DD_{integrationName}_ANALYTICS_SAMPLE_RATE\"");
        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetToStringFast(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        sb.Clear();
        foreach (var member in enumToGenerate.Names)
        {
            sb.AppendLine()
              .Append("            ")
              .Append(enumToGenerate.FullyQualifiedName)
              .Append('.')
              .Append(member.Property)
              .Append(" => ");

            AppendDescription(sb, member, enumToGenerate.FullyQualifiedName);
            sb.Append(',');
        }

        return sb.ToString();
    }

    private static void AppendDescription(StringBuilder sb, (string Property, string? Name) member, string fullyQualifiedName)
    {
        if (member.Name is null)
        {
            sb.Append("nameof(").Append(fullyQualifiedName).Append('.').Append(member.Property).Append(")");
        }
        else
        {
            sb.Append('"').Append(member.Name).Append('"');
        }
    }

    private static string GetValues(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        sb.Clear();
        foreach (var member in enumToGenerate.Names)
        {
            sb.AppendLine()
              .Append("            ")
              .Append(enumToGenerate.FullyQualifiedName)
              .Append('.')
              .Append(member.Property)
              .Append(',');
        }

        return sb.ToString();
    }

    private static string GetNames(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        sb.Clear();
        foreach (var member in enumToGenerate.Names)
        {
            sb.AppendLine()
              .Append("            nameof(")
              .Append(enumToGenerate.FullyQualifiedName)
              .Append('.')
              .Append(member.Property)
              .Append("),");
        }

        return sb.ToString();
    }

    private static string GetDescriptions(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        if (!enumToGenerate.HasDescriptions)
        {
            return string.Empty;
        }

        sb.Clear();
        sb.AppendLine()
          .Append(
            $$"""

            /// <summary>
            /// Retrieves an array of the names of the members defined in
            /// <see cref="{{enumToGenerate.FullyQualifiedName}}" />.
            /// Note that this returns a new array with every invocation, so
            /// should be cached if appropriate.
            /// Uses <c>[Description]</c> definition if available, otherwise uses the name of the property
            /// </summary>
            /// <returns>An array of the names of the members defined in <see cref="{{enumToGenerate.FullyQualifiedName}}" /></returns>
            public static string[] GetDescriptions()
                => new []
                {
        """);

        foreach (var member in enumToGenerate.Names)
        {
            sb.AppendLine()
              .Append("            ");
            AppendDescription(sb, member, enumToGenerate.FullyQualifiedName);
            sb.Append(',');
        }

        sb.AppendLine()
          .Append("        };");
        return sb.ToString();
    }

    private static string GetHasFlags(in EnumToGenerate enumToGenerate)
    {
        if (!enumToGenerate.HasFlags)
        {
            return string.Empty;
        }

        return $$"""

                /// <summary>
                /// Determines whether one or more bit fields are set in the current instance.
                /// Equivalent to calling <see cref="System.Enum.HasFlag" /> on <paramref name="value"/>.
                /// </summary>
                /// <param name="value">The value of the instance to investigate</param>
                /// <param name="flag">The flag to check for</param>
                /// <returns><c>true</c> if the fields set in the flag are also set in the current instance; otherwise <c>false</c>.</returns>
                /// <remarks>If the underlying value of <paramref name="flag"/> is zero, the method returns true.
                /// This is consistent with the behaviour of <see cref="System.Enum.HasFlag" /></remarks>
                public static bool HasFlagFast(this {{enumToGenerate.FullyQualifiedName}} value, {{enumToGenerate.FullyQualifiedName}} flag)
                    => flag == 0 ? true : (value & flag) == flag;
        """;
    }
}
