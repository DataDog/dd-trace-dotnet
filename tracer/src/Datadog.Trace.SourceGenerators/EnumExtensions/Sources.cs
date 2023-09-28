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
