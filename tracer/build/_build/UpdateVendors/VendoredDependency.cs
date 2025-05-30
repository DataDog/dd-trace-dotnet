// <copyright file="VendoredDependency.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UpdateVendors
{
    public class VendoredDependency
    {
        private const string AutoGeneratedMessage = @"//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendoredCode tool.
//------------------------------------------------------------------------------
";

        static VendoredDependency()
        {
            Add(
                libraryName: "Microsoft.OpenApi",
                "1.6.23",
                "https://github.com/microsoft/OpenAPI.NET/archive/1.6.23.zip",
                new[] { "OpenAPI.NET-1.6.23", "src", "Microsoft.OpenApi" },
                filePath => RewriteCsFileWithStandardTransform(filePath, "Microsoft.OpenApi"),
                new[]
                {
                    "Extensions/OpenApiElementExtensions.cs",
                    "Extensions/OpenApiSerializableExtensions.cs",
                    "Extensions/OpenApiTypeMapper.cs",
                    "MicrosoftExtensions/", // whole folder
                    "Services/CopyReferences.cs",
                    "Services/OpenApiFilterService.cs",
                    "Services/OpenApiUrlTreeNode.cs",
                    "Services/OperationSearch.cs",
                    "Services/SearchResult.cs",
                    "Validations/", // whole folder
                    "Writers/FormattingStreamWriter.cs",
                    "Writers/OpenApiYamlWriter.cs"
                });

            Add(
                libraryName: "Serilog",
                version: "2.10.0",
                downloadUrl: "https://github.com/serilog/serilog/archive/v2.10.0.zip",
                pathToSrc: new[] { "serilog-2.10.0", "src", "Serilog" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "Serilog"));

            Add(
                libraryName: "Serilog.Sinks.File",
                version: "4.1.0",
                downloadUrl: "https://github.com/serilog/serilog-sinks-file/archive/v4.1.0.zip",
                pathToSrc: new[] { "serilog-sinks-file-4.1.0", "src", "Serilog.Sinks.File" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "Serilog"));

            Add(
                libraryName: "StatsdClient",
                version: "6.0.0",
                downloadUrl: "https://github.com/DataDog/dogstatsd-csharp-client/archive/6.0.0.zip",
                pathToSrc: new[] { "dogstatsd-csharp-client-6.0.0", "src", "StatsdClient" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "StatsdClient"));

            Add(
                libraryName: "MessagePack",
                version: "1.9.11",
                downloadUrl: "https://github.com/neuecc/MessagePack-CSharp/archive/refs/tags/v1.9.11.zip",
                pathToSrc: new[] { "MessagePack-CSharp-1.9.11", "src", "MessagePack" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "MessagePack"));

            Add(
                libraryName: "Newtonsoft.Json",
                version: "13.0.2",
                downloadUrl: "https://github.com/JamesNK/Newtonsoft.Json/archive/13.0.2.zip",
                pathToSrc: new[] { "Newtonsoft.Json-13.0.2", "src", "Newtonsoft.Json" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "Newtonsoft.Json", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma),
                relativePathsToExclude: new[] { "Utilities/NullableAttributes.cs" });

            Add(
                libraryName: "dnlib",
                version: "3.4.0",
                downloadUrl: "https://github.com/0xd4d/dnlib/archive/refs/tags/v3.4.0.zip",
                pathToSrc: new[] { "dnlib-3.4.0", "src" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "dnlib"));

            Add(
                libraryName: "Datadog.Sketches",
                version: "1.0.0",
                downloadUrl: "https://github.com/DataDog/sketches-dotnet/archive/v1.0.0.zip",
                pathToSrc: new[] { "sketches-dotnet-1.0.0", "src", "Datadog.Sketches" },
                // Perform standard CS file transform with additional '#nullable enable' directive at the beginning of the files, since the vendored project was built with <Nullable>enable</Nullable>
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "Datadog.Sketches", AddNullableDirectiveTransform));

            Add(
                libraryName: "System.Collections.Immutable",
                version: "7.0.0",
                downloadUrl: "https://github.com/DataDog/dotnet-vendored-code/archive/refs/tags/1.0.0.zip",
                pathToSrc: new[] { "dotnet-vendored-code-1.0.0", "System.Reflection.Metadata", "System.Collections.Immutable" },
                transform: filePath =>
                {
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Collections.", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                });

            Add(
                libraryName: "System.Memory",
                version: "4.5.5",
                downloadUrl: "https://github.com/DataDog/dotnet-vendored-code/archive/refs/tags/1.0.0.zip",
                pathToSrc: new[] { "dotnet-vendored-code-1.0.0", "System.Reflection.Metadata", "System.Memory" },
                transform: filePath =>
                {
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                },
                relativePathsToExclude: new[] { "Buffers/ArrayPoolEventSource.cs" });

            Add(
                libraryName: "System.Private.CoreLib",
                version: "1.0.0",
                downloadUrl: "https://github.com/DataDog/dotnet-vendored-code/archive/refs/tags/1.0.0.zip",
                pathToSrc: new[] { "dotnet-vendored-code-1.0.0", "System.Reflection.Metadata", "System.Private.CoreLib" },
                transform: filePath =>
                {
                    
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Runtime", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Diagnostics", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "FxResources", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                });

            Add(
                libraryName: "System.Reflection.Metadata",
                version: "7.0.2",
                downloadUrl: "https://github.com/DataDog/dotnet-vendored-code/archive/refs/tags/1.0.0.zip",
                pathToSrc: new[] { "dotnet-vendored-code-1.0.0", "System.Reflection.Metadata", "System.Reflection.Metadata" },
                transform: filePath =>
                {
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Reflection.", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Collections.", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                    RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Runtime.", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma);
                });

            Add(
                libraryName: "System.Runtime.CompilerServices.Unsafe",
                version: "1.0.0",
                downloadUrl: "https://github.com/DataDog/dotnet-vendored-code/archive/refs/tags/1.0.0.zip",
                pathToSrc: new[] { "dotnet-vendored-code-1.0.0", "System.Reflection.Metadata", "System.Runtime.CompilerServices.Unsafe" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "System.Runtime", AddNullableDirectiveTransform, AddIgnoreNullabilityWarningDisablePragma));

            Add(
                libraryName: "ICSharpCode.SharpZipLib",
                version: "1.3.3",
                downloadUrl: "https://github.com/icsharpcode/SharpZipLib/archive/refs/tags/v1.3.3.zip",
                pathToSrc: new[] { "SharpZipLib-1.3.3", "src", "ICSharpCode.SharpZipLib" },
                transform: filePath => RewriteCsFileWithStandardTransform(filePath, originalNamespace: "ICSharpCode.SharpZipLib", AddIfNetFramework));
        }

        public static List<VendoredDependency> All { get; set; } = new List<VendoredDependency>();

        public string LibraryName { get; set; }

        public string Version { get; set; }

        public string DownloadUrl { get; set; }

        public string[] PathToSrc { get; set; }

        public Action<string> Transform { get; set; }

        public string[] RelativePathsToExclude { get; set; }

        private static void Add(
            string libraryName,
            string version,
            string downloadUrl,
            string[] pathToSrc,
            Action<string> transform,
            string[] relativePathsToExclude = null)
        {
            All.Add(new VendoredDependency()
            {
                LibraryName = libraryName,
                Version = version,
                DownloadUrl = downloadUrl,
                PathToSrc = pathToSrc,
                Transform = transform,
                RelativePathsToExclude = relativePathsToExclude ?? Array.Empty<string>(),
            });
        }

        private static string AddIfNetcoreapp31OrGreater(string filePath, string content)
        {
            return "#if NETCOREAPP3_1_OR_GREATER" + Environment.NewLine + content + Environment.NewLine + "#endif";
        }

        private static string AddIfNetFramework(string filePath, string content)
        {
            return "#if NETFRAMEWORK" + Environment.NewLine + content + Environment.NewLine + "#endif";
        }

        private static string AddNullableDirectiveTransform(string filePath, string content)
        {
            if (!content.Contains("#nullable"))
            {
                return "#nullable enable" + Environment.NewLine + content;
            }

            return content;
        }

        private static void RewriteCsFileWithStandardTransform(string filePath, string originalNamespace, params Func<string, string, string>[] extraTransform)
        {
            if (string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                RewriteFileWithTransform(
                    filePath,
                    content =>
                    {
                        foreach (var transform in extraTransform)
                        {
                            if (transform != null)
                            {
                                content = transform(filePath, content);
                            }
                        }

                        // Disable analyzer
                        var builder = new StringBuilder(AutoGeneratedMessage, content.Length * 2);
                        builder.AppendLine(GenerateWarningDisablePragma());
                        builder.Append(content);

                        // Special Newtonsoft.Json processing
                        if (originalNamespace.Equals("Newtonsoft.Json"))
                        {
                            builder.Replace($"using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs", "using ErrorEventArgs = Datadog.Trace.Vendors.Newtonsoft.Json.Serialization.ErrorEventArgs");

                            if (content.Contains("using Newtonsoft.Json.Serialization;"))
                            {
                                builder.Replace($"Func<", $"System.Func<");
                                builder.Replace($"Action<", $"System.Action<");
                            }

                            var filename = Path.GetFileName(filePath);
                            if (filename == "JsonSerializerInternalReader.cs")
                            {
                                builder.Replace("#pragma warning restore CS8600, CS8602, CS8603, CS8604", string.Empty);
                            }

                            builder.Replace("#if !(PORTABLE40 || PORTABLE || DOTNET || NETSTANDARD2_0)", "#if NETFRAMEWORK");

                            if (filename == "JsonPropertyCollection.cs")
                            {
                                // eww
                                builder.Replace(
                                    @"        private bool TryGetValue(string key, [NotNullWhen(true)]out JsonProperty? item)",
                                    @"#if NETCOREAPP
        private new bool TryGetValue(string key, [NotNullWhen(true)]out JsonProperty? item)
#else
        private bool TryGetValue(string key, [NotNullWhen(true)]out JsonProperty? item)
#endif");
                            }
                        }


                        if (originalNamespace.Equals("dnlib"))
                        {
                            // dnlib's only targets net461 and netstandard2.0.
                            // For our needs, it's more correct to consider `NETSTANDARD` as 'everything not .NET Framework'
                            builder.Replace("#if NETSTANDARD", "#if !NETFRAMEWORK");

                            // Make certain classes partial so we can extend them.
                            foreach (var className in new[] { "SymbolReaderImpl", "PdbReader", "PortablePdbReader" })
                            {
                                builder.Replace($"class {className}", $"partial class {className}");
                            }
                        }

                        // Special MessagePack processing
                        if (originalNamespace.StartsWith("MessagePack"))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            if (fileName == "StandardClassLibraryFormatter")
                            {
                                builder.Replace("    public sealed class ValueTaskFormatter<T>", "#if NETCOREAPP\n    public sealed class ValueTaskFormatter<T>");
                                builder.Replace("    }\n\n#endif\n}", "    }\n#endif\n\n#endif\n}");
                            }
                            else if (fileName == "ValueTupleFormatter")
                            {
                                builder.Replace("#if NETSTANDARD || NETFRAMEWORK", "#if NETSTANDARD || NETCOREAPP");
                            }
                            else if (fileName == "DynamicAssembly")
                            {
                                builder.Replace("#if NETSTANDARD || NETFRAMEWORK", "#if NETSTANDARD || NETFRAMEWORK || NETCOREAPP");
                            }
                            else if (fileName == "LZ4Codec.Helper")
                            {
                                builder.Replace("#if NETSTANDARD || NETFRAMEWORK", "#if ENABLE_UNSAFE_MSGPACK");
                            }
                            else if (fileName == "StandardResolver")
                            {
                                builder.Replace("#if !(NETSTANDARD || NETFRAMEWORK)", "#if !(NETSTANDARD || NETFRAMEWORK || NETCOREAPP)");
                            }
                            else if (fileName == "DynamicGenericResolver")
                            {
                                builder.Replace("                // ValueTask", "#if NETCOREAPP\n                // ValueTask");
                                builder.Replace("                // ValueTuple", "#if NETCOREAPP\n                // ValueTuple");
                                builder.Replace("                // Tuple", "#endif\n                // Tuple");
                                builder.Replace("                // ArraySegement", "#endif\n                // ArraySegment");
                                builder.Replace("GetTypeInfo().IsConstructedGenericType()", "IsConstructedGenericType");
                            }

                            builder.Replace("#if NETSTANDARD || NETFRAMEWORK\n        [System.Runtime.CompilerServices.MethodImpl", "#if NETSTANDARD || NETFRAMEWORK || NETCOREAPP\n        [System.Runtime.CompilerServices.MethodImpl");
                        }

                        // Special SharpZipLib processing
                        if (originalNamespace.StartsWith("ICSharpCode"))
                        {
                            builder.Replace("#if NET45", "#if NETFRAMEWORK");
                            builder.Replace("using static ICSharpCode.SharpZipLib", "using static Datadog.Trace.Vendors.ICSharpCode.SharpZipLib");
                        }

                        // Debugger.Break() is a dangerous method that may crash the process. We don't
                        // want to take any risk of calling it, ever, so replace it with a noop.
                        builder.Replace("Debugger.Break();", "{}");

                        string datadogVendoredNamespace = originalNamespace.StartsWith("System") ? "Datadog.Trace.VendoredMicrosoftCode." : "Datadog.Trace.Vendors.";

                        // Prevent namespace conflicts
                        builder.Replace($"using {originalNamespace}", $"using {datadogVendoredNamespace}{originalNamespace}");
                        builder.Replace($"namespace {originalNamespace}", $"namespace {datadogVendoredNamespace}{originalNamespace}");
                        builder.Replace($"[CLSCompliant(false)]", $"// [CLSCompliant(false)]");

                        // Fix namespace conflicts in `using alias` directives. For example, transform:
                        //      using Foo = dnlib.A.B.C;
                        // To:
                        //      using Foo = Datadog.Trace.Vendors.dnlib.A.B.C;
                        string result =
                            Regex.Replace(
                                builder.ToString(),
                                @$"using\s+(\S+)\s+=\s+{Regex.Escape(originalNamespace)}.(.*);",
                                match => $"using {match.Groups[1].Value} = {datadogVendoredNamespace}{originalNamespace}.{match.Groups[2].Value};");


                        // Don't expose anything we don't intend to
                        // by replacing all "public" access modifiers with "internal"
                        return Regex.Replace(
                            result,
                            @"public(\s+((abstract|sealed|static|unsafe)\s+)*?(partial\s+)?(class|readonly\s+(ref\s+)?struct|struct|interface|enum|delegate))",
                            match => $"internal{match.Groups[1]}");
                    });
            }
        }

        static string GenerateWarningDisablePragma() =>
            "#pragma warning disable " +
            "CS0618, " +      // Type or member is obsolete
            "CS0649, " +      // Field is never assigned to, and will always have its default value
            "CS1574, " +      // XML comment has a cref attribute that could not be resolved
            "CS1580, " +      // Invalid type for parameter in XML comment cref attribute
            "CS1581, " +      // Invalid return type in XML comment cref attribute
            "CS1584, " +      // XML comment has syntactically incorrect cref attribute
            "CS1591, " +      // Missing XML comment for publicly visible type or member 'x'
            "CS1573, " +      // Parameter 'x' has no matching param tag in the XML comment for 'y' (but other parameters do)
            "CS8018, " +      // Within cref attributes, nested types of generic types should be qualified
            "SYSLIB0011, " +  // BinaryFormatter serialization is obsolete and should not be used.
            "SYSLIB0023, " +  // RNGCryptoServiceProvider is obsolete. To generate a random number, use one of the RandomNumberGenerator static methods instead.
            "SYSLIB0032";     // Recovery from corrupted process state exceptions is not supported; HandleProcessCorruptedStateExceptionsAttribute is ignored."

        static string AddIgnoreNullabilityWarningDisablePragma(string filePath, string content) =>
            "#pragma warning disable " +
            "CS8600, " + // Converting null literal or possible null value to non-nullable type.
            "CS8601, " + // Possible null reference assignment
            "CS8602, " + // Dereference of a possibly null reference
            "CS8603, " + // Possible null reference return
            "CS8604, " + // Possible null reference argument for parameter 'x' in 'y'
            "CS8618, " + // Non-nullable field 'x' must contain a non-null value when exiting constructor. Consider declaring the field as nullable.
            "CS8620, " + // Argument of type 'x' cannot be used for parameter 'y' of type 'z[]' in 'a' due to differences in the nullability of reference types.
            "CS8714, " + // The type 'x' cannot be used as type parameter 'y' in the generic type or method 'z'. Nullability of type argument 'x' doesn't match 'notnull' constraint.
            "CS8762, " + // Parameter 'x' must have a non-null value when exiting with 'true'
            "CS8765, " + // Nullability of type of parameter 'x' doesn't match overridden member (possibly because of nullability attributes)
            "CS8766, " + // Nullability of reference types in return type of 'x' doesn't match implicitly implemented member 'y' (possibly because of nullability attributes)
            "CS8767, " + // Nullability of reference types in type of parameter 'x' of 'y' doesn't match implicitly implemented member 'z' (possibly because of nullability attributes)
            "CS8768, " + // Nullability of reference types in return type doesn't match implemented member 'x' (possibly because of nullability attributes)
            "CS8769, " + // Nullability of reference types in type of parameter 'x' doesn't match implemented member 'y'  (possibly because of nullability attributes)
            "CS8612, " + // Nullability of reference types in type of 'x' doesn't match implicitly implemented member 'y'.
            "CS8629, " + // Nullable value type may be null with temporary variables
            "CS8774" +   // Member 'x' must have a non-null value when exiting.
            Environment.NewLine + content;

        private static void RewriteFileWithTransform(string filePath, Func<string, string> transform)
        {
            var fileContent = File.ReadAllText(filePath);
            fileContent = transform(fileContent);
            // Normalize text to use CRLF line endings so we have deterministic results
            fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(
                filePath,
                fileContent,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
