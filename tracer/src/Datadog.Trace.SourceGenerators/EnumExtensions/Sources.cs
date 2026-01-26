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

    public static string GenerateExtensionClass(StringBuilder sb, in EnumToGenerate enumToGenerate) =>
        Constants.FileHeader + $$"""
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

    public static string GenerateIntegrationNameToKeys(StringBuilder sb, in EnumToGenerate enumToGenerate)
    {
        sb.Clear();
        sb.Append(
            Constants.FileHeader +
            """

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
                    [Datadog.Trace.SourceGenerators.TestingOnly]
                    public static string[] GetAllIntegrationEnabledKeys() =>
                    [

            """);

        foreach (var member in enumToGenerate.Names)
        {
            var name = member.Property;
            var upperName = name.ToUpperInvariant();

            var upperKey = $"DD_TRACE_{upperName}_ENABLED";
            var mixedKey = $"DD_TRACE_{name}_ENABLED";
            var shortKey = $"DD_{name}_ENABLED";
            sb.AppendLine($"            \"{upperKey}\", \"{mixedKey}\", \"{shortKey}\",");

            var analyticsUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_ENABLED";
            var analyticsMixedKey = $"DD_TRACE_{name}_ANALYTICS_ENABLED";
            var analyticsShortKey = $"DD_{name}_ANALYTICS_ENABLED";
            sb.AppendLine($"            \"{analyticsUpperKey}\", \"{analyticsMixedKey}\", \"{analyticsShortKey}\",");

            var sampleRateUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_SAMPLE_RATE";
            var sampleRateMixedKey = $"DD_TRACE_{name}_ANALYTICS_SAMPLE_RATE";
            var sampleRateShortKey = $"DD_{name}_ANALYTICS_SAMPLE_RATE";
            sb.AppendLine($"            \"{sampleRateUpperKey}\", \"{sampleRateMixedKey}\", \"{sampleRateShortKey}\", ");
        }

        sb.Append(
            """
                    ];
                    /// <summary>
                    /// Gets the configuration keys for the specified integration name.
                    /// Returns a KeyValuePair where Key is the canonical key and Value is an array of aliases.
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>KeyValuePair with canonical key and aliases</returns>
                    public static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationEnabledKeys(string integrationName) =>
                        integrationName switch
                        {

            """);

        foreach (var member in enumToGenerate.Names)
        {
            var name = member.Property;
            var upperName = name.ToUpperInvariant();
            var upperKey = $"DD_TRACE_{upperName}_ENABLED";
            var mixedKey = $"DD_TRACE_{name}_ENABLED";
            var shortKey = $"DD_{name}_ENABLED";
            sb.AppendLine($"                \"{name}\" => new(\"{upperKey}\", [\"{mixedKey}\", \"{shortKey}\"]),");
        }

        sb.Append(
            """
                            _ => GetIntegrationEnabledKeysFallback(integrationName) // we should never get here
                        };
                    /// <summary>
                    /// Gets the analytics enabled configuration keys for the specified integration name.
                    /// Returns a KeyValuePair where Key is the canonical key and Value is an array of aliases.
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>KeyValuePair with canonical key and aliases</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationAnalyticsEnabledKeys(string integrationName) =>
                        integrationName switch
                        {

            """);

        foreach (var member in enumToGenerate.Names)
        {
            var name = member.Property;
            var upperName = name.ToUpperInvariant();
            var analyticsUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_ENABLED";
            var analyticsMixedKey = $"DD_TRACE_{name}_ANALYTICS_ENABLED";
            var analyticsShortKey = $"DD_{name}_ANALYTICS_ENABLED";
            sb.AppendLine($"                \"{name}\" => new(\"{analyticsUpperKey}\", [\"{analyticsMixedKey}\", \"{analyticsShortKey}\"]),");
        }

        sb.Append(
            """
                            _ => GetIntegrationAnalyticsEnabledKeysFallback(integrationName) // we should never get here
                        };
                    /// <summary>
                    /// Gets the analytics sample rate configuration keys for the specified integration name.
                    /// Returns a KeyValuePair where Key is the canonical key and Value is an array of aliases.
                    /// </summary>
                    /// <param name="integrationName">The integration name</param>
                    /// <returns>KeyValuePair with canonical key and aliases</returns>
                    [System.Obsolete(ObsoleteMessage)]
                    public static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationAnalyticsSampleRateKeys(string integrationName) =>
                        integrationName switch
                        {

            """);

        foreach (var member in enumToGenerate.Names)
        {
            var name = member.Property;
            var upperName = name.ToUpperInvariant();
            var sampleRateUpperKey = $"DD_TRACE_{upperName}_ANALYTICS_SAMPLE_RATE";
            var sampleRateMixedKey = $"DD_TRACE_{name}_ANALYTICS_SAMPLE_RATE";
            var sampleRateShortKey = $"DD_{name}_ANALYTICS_SAMPLE_RATE";
            sb.AppendLine($"                \"{name}\" => new(\"{sampleRateUpperKey}\", [\"{sampleRateMixedKey}\", \"{sampleRateShortKey}\"]),");
        }

        sb.Append(
            """
                            _ => GetIntegrationAnalyticsSampleRateKeysFallback(integrationName) // we should never get here
                        };

                    private static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationEnabledKeysFallback(string integrationName) =>
                        new(string.Format("DD_TRACE_{0}_ENABLED", integrationName.ToUpperInvariant()),
                        [
                            string.Format("DD_TRACE_{0}_ENABLED", integrationName),
                            $"DD_{integrationName}_ENABLED"
                        ]);

                    private static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationAnalyticsEnabledKeysFallback(string integrationName) =>
                        new(string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName.ToUpperInvariant()),
                        [
                            string.Format("DD_TRACE_{0}_ANALYTICS_ENABLED", integrationName),
                            $"DD_{integrationName}_ANALYTICS_ENABLED"
                        ]);

                    private static System.Collections.Generic.KeyValuePair<string, string[]> GetIntegrationAnalyticsSampleRateKeysFallback(string integrationName) =>
                        new(string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName.ToUpperInvariant()),
                        [
                            string.Format("DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE", integrationName),
                            $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE"
                        ]);
                }
            }
            
            """);

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
