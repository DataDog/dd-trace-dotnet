// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.SourceGenerators.PublicApi;

internal class Sources
{
    public const string Attributes = Constants.FileHeader + """
        namespace Datadog.Trace.SourceGenerators;

        /// <summary>
        /// Used to generate a public property for a decorated field,
        /// allowing adding aspect-oriented changes such as telemetry etc.
        /// Any documentation added to the field is copied to the public API
        /// </summary>
        [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
        internal class GeneratePublicApiAttribute : System.Attribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PublicApiAttribute"/> class.
            /// Adds a getter and a setter.
            /// </summary>
            /// <param name="getApiUsage">Gets the name of the public API used for the property getter</param>
            /// <param name="setApiUsage">Gets the name of the public API used for the property setter</param>
            public GeneratePublicApiAttribute(
                Datadog.Trace.Telemetry.Metrics.PublicApiUsage getApiUsage,
                Datadog.Trace.Telemetry.Metrics.PublicApiUsage setApiUsage)
            {
                Getter = getApiUsage;
                Setter = setApiUsage;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="PublicApiAttribute"/> class.
            /// Adds a getter only.
            /// </summary>
            /// <param name="getApiUsage">Gets the name of the public API used for the property getter. If null, no getter will be generated.</param>
            public GeneratePublicApiAttribute(Datadog.Trace.Telemetry.Metrics.PublicApiUsage getApiUsage)
            {
                Getter = getApiUsage;
            }

            /// <summary>
            /// Gets the name of the public API used for the getter
            /// </summary>
            public Datadog.Trace.Telemetry.Metrics.PublicApiUsage Getter { get; }

            /// <summary>
            /// Gets the name of the public API used for the setter
            /// </summary>
            public Datadog.Trace.Telemetry.Metrics.PublicApiUsage? Setter { get; }
        }

        /// <summary>
        /// A marker attribute added to a public API to indicate it should only be
        /// called by consumers. Used by analyzers to confirm we're not calling a public API method.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        [System.AttributeUsage(
            System.AttributeTargets.Field
          | System.AttributeTargets.Property
          | System.AttributeTargets.Method
          | System.AttributeTargets.Constructor)]
        internal sealed class PublicApiAttribute : System.Attribute
        {
        }

        """;

    public static string CreatePartialClass(StringBuilder sb, string nameSpace, string className, bool isRecord, IEnumerable<PublicApiGenerator.PublicApiProperty> properties)
    {
        var classKeyword = isRecord ? "record" : "class";

        return Constants.FileHeader + $$"""
            namespace {{nameSpace}};
            partial {{classKeyword}} {{className}}
            {{{GetProperties(sb, properties)}}
            }
            """;
    }

    private static string GetProperties(StringBuilder sb, IEnumerable<PublicApiGenerator.PublicApiProperty> properties)
    {
        foreach (var property in properties)
        {
            sb.AppendLine();
            // The leading trivia may have arbitrary whitespace, so the indentation might not be right here, but
            // it's not worth worrying about IMO
            if (!string.IsNullOrWhiteSpace(property.LeadingTrivia))
            {
                sb.AppendLine(property.LeadingTrivia.TrimEnd());
            }

            if (property.ObsoleteMessage is { } obsolete)
            {
                sb.Append("    [System.Obsolete");
                if (obsolete != string.Empty)
                {
                    sb.Append("(\"").Append(obsolete).Append("\")");
                }

                sb.Append(']').AppendLine();
            }

            sb.AppendLine(
                $$"""
                    [Datadog.Trace.SourceGenerators.PublicApi]
                    public {{property.ReturnType}} {{property.PropertyName}}
                    {
                        get
                        {
                            Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                                (Datadog.Trace.Telemetry.Metrics.PublicApiUsage){{property.PublicApiGetter}});
                            return {{property.FieldName}};
                        }
                """);
            if (property.PublicApiSetter.HasValue)
            {
                sb.AppendLine(
                    $$"""
                        set
                        {
                            Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                                (Datadog.Trace.Telemetry.Metrics.PublicApiUsage){{property.PublicApiSetter}});
                            {{property.FieldName}} = value;
                        }
                """);
            }

            sb.Append("    }");
        }

        return sb.ToString();
    }
}
