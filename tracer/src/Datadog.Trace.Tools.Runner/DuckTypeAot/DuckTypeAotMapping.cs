// <copyright file="DuckTypeAotMapping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1204 // Static elements should appear before instance elements

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Defines named constants for duck type aot mapping mode.
    /// </summary>
    internal enum DuckTypeAotMappingMode
    {
        /// <summary>
        /// Represents forward.
        /// </summary>
        Forward,

        /// <summary>
        /// Represents reverse.
        /// </summary>
        Reverse
    }

    /// <summary>
    /// Defines named constants for duck type aot mapping source.
    /// </summary>
    internal enum DuckTypeAotMappingSource
    {
        /// <summary>
        /// Represents attribute.
        /// </summary>
        Attribute,

        /// <summary>
        /// Represents map file.
        /// </summary>
        MapFile
    }

    /// <summary>
    /// Defines named constants for runtime registration kind.
    /// </summary>
    internal enum DuckTypeAotRuntimeRegistrationKind
    {
        /// <summary>
        /// Represents a canonical mapping registration.
        /// </summary>
        Canonical,

        /// <summary>
        /// Represents an assignable alias registration.
        /// </summary>
        AssignableAlias,

        /// <summary>
        /// Represents a nullable alias registration.
        /// </summary>
        NullableAlias
    }

    /// <summary>
    /// Represents duck type aot mapping.
    /// </summary>
    internal sealed class DuckTypeAotMapping
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMapping"/> class.
        /// </summary>
        /// <param name="proxyTypeName">The proxy type name value.</param>
        /// <param name="proxyAssemblyName">The proxy assembly name value.</param>
        /// <param name="targetTypeName">The target type name value.</param>
        /// <param name="targetAssemblyName">The target assembly name value.</param>
        /// <param name="mode">The mode value.</param>
        /// <param name="source">The source value.</param>
        /// <param name="scenarioId">The scenario id value.</param>
        public DuckTypeAotMapping(
            string proxyTypeName,
            string proxyAssemblyName,
            string targetTypeName,
            string targetAssemblyName,
            DuckTypeAotMappingMode mode,
            DuckTypeAotMappingSource source,
            string? scenarioId = null)
        {
            // Canonical names make every spelling of a type pair (recorded, expanded from generic roots...) the same mapping.
            ProxyTypeName = DuckTypeAotNameHelpers.CanonicalizeTypeName(proxyTypeName);
            ProxyAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(proxyAssemblyName);
            TargetTypeName = DuckTypeAotNameHelpers.CanonicalizeTypeName(targetTypeName);
            TargetAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(targetAssemblyName);
            Mode = mode;
            Source = source;
            ScenarioId = NormalizeScenarioId(scenarioId);
        }

        /// <summary>
        /// Gets proxy type name.
        /// </summary>
        /// <value>The proxy type name value.</value>
        public string ProxyTypeName { get; }

        /// <summary>
        /// Gets proxy assembly name.
        /// </summary>
        /// <value>The proxy assembly name value.</value>
        public string ProxyAssemblyName { get; }

        /// <summary>
        /// Gets target type name.
        /// </summary>
        /// <value>The target type name value.</value>
        public string TargetTypeName { get; }

        /// <summary>
        /// Gets target assembly name.
        /// </summary>
        /// <value>The target assembly name value.</value>
        public string TargetAssemblyName { get; }

        /// <summary>
        /// Gets mode.
        /// </summary>
        /// <value>The mode value.</value>
        public DuckTypeAotMappingMode Mode { get; }

        /// <summary>
        /// Gets source.
        /// </summary>
        /// <value>The source value.</value>
        public DuckTypeAotMappingSource Source { get; }

        /// <summary>
        /// Gets scenario id.
        /// </summary>
        /// <value>The scenario id value.</value>
        public string? ScenarioId { get; }

        public string Key =>
            string.Concat(
                Mode.ToString(),
                "|",
                ProxyAssemblyName.ToUpperInvariant(),
                "|",
                ProxyTypeName,
                "|",
                TargetAssemblyName.ToUpperInvariant(),
                "|",
                TargetTypeName);

        /// <summary>
        /// Executes with scenario id.
        /// </summary>
        /// <param name="scenarioId">The scenario id value.</param>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMapping WithScenarioId(string scenarioId)
        {
            return new DuckTypeAotMapping(
                ProxyTypeName,
                ProxyAssemblyName,
                TargetTypeName,
                TargetAssemblyName,
                Mode,
                Source,
                scenarioId);
        }

        /// <summary>
        /// Normalizes normalize scenario id.
        /// </summary>
        /// <param name="scenarioId">The scenario id value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static string? NormalizeScenarioId(string? scenarioId)
        {
            var trimmedScenarioId = scenarioId?.Trim();
            // Branch: take this path when (string.IsNullOrWhiteSpace(trimmedScenarioId)) evaluates to true.
            if (string.IsNullOrWhiteSpace(trimmedScenarioId))
            {
                return null;
            }

            return trimmedScenarioId;
        }
    }

    /// <summary>
    /// Represents a runtime registration emitted into the generated AOT registry.
    /// </summary>
    internal sealed class DuckTypeAotRuntimeRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotRuntimeRegistration"/> class.
        /// </summary>
        /// <param name="mapping">The runtime mapping value.</param>
        /// <param name="canonicalMappingKey">The canonical mapping key value.</param>
        /// <param name="kind">The registration kind value.</param>
        public DuckTypeAotRuntimeRegistration(DuckTypeAotMapping mapping, string canonicalMappingKey, DuckTypeAotRuntimeRegistrationKind kind)
        {
            Mapping = mapping;
            CanonicalMappingKey = canonicalMappingKey;
            Kind = kind;
        }

        /// <summary>
        /// Gets runtime mapping.
        /// </summary>
        public DuckTypeAotMapping Mapping { get; }

        /// <summary>
        /// Gets canonical mapping key.
        /// </summary>
        public string CanonicalMappingKey { get; }

        /// <summary>
        /// Gets registration kind.
        /// </summary>
        public DuckTypeAotRuntimeRegistrationKind Kind { get; }

        /// <summary>
        /// Gets a value indicating whether the registration is canonical.
        /// </summary>
        public bool IsCanonical => Kind == DuckTypeAotRuntimeRegistrationKind.Canonical;
    }

    /// <summary>
    /// Provides helper operations for duck type aot name helpers.
    /// </summary>
    internal static class DuckTypeAotNameHelpers
    {
        /// <summary>
        /// Normalizes normalize assembly name.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <returns>The resulting string value.</returns>
        internal static string NormalizeAssemblyName(string assemblyName)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            var commaIndex = assemblyName.IndexOf(',');
            return commaIndex >= 0 ? assemblyName.Substring(0, commaIndex).Trim() : assemblyName.Trim();
        }

        /// <summary>
        /// Gets the canonical spelling of a reflection type name: nested types are separated with '+', and the generic
        /// arguments keep only their simple assembly name (Type.FullName adds Version, Culture and PublicKeyToken).
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The canonical type name, or the trimmed name if it can't be parsed.</returns>
        internal static string CanonicalizeTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return typeName ?? string.Empty;
            }

            var trimmedTypeName = typeName.Trim();
            if (trimmedTypeName.IndexOf('[') < 0)
            {
                return trimmedTypeName.Replace('/', '+');
            }

            var index = 0;
            return TryCanonicalizeTypeName(trimmedTypeName, ref index, out var canonicalTypeName) && index == trimmedTypeName.Length
                       ? canonicalTypeName
                       : trimmedTypeName;
        }

        /// <summary>
        /// Gets the name Type.ToString() gives a type from its reflection name: '+' between nested types, and generic arguments
        /// without their assemblies (e.g. "Ns.Box`1[System.Int32]"), like in the messages of dynamic duck typing.
        /// </summary>
        /// <param name="typeName">The reflection name of the type, with or without assembly qualified generic arguments.</param>
        /// <returns>The name of the type as Type.ToString() writes it.</returns>
        internal static string ToTypeToStringName(string typeName)
        {
            var index = 0;
            var name = ReadTypeToStringName(typeName.Replace('/', '+'), ref index);
            return index == typeName.Length ? name : typeName.Replace('/', '+');

            static string ReadTypeToStringName(string text, ref int index)
            {
                var builder = new StringBuilder();
                while (index < text.Length && text[index] != ',' && text[index] != ']')
                {
                    if (text[index] != '[')
                    {
                        builder.Append(text[index++]);
                    }
                    else if (index + 1 < text.Length && text[index + 1] == '[')
                    {
                        // Assembly qualified generic arguments: [[Type, Assembly],[Type, Assembly]].
                        index++;
                        builder.Append('[');
                        while (index < text.Length && text[index] == '[')
                        {
                            index++;
                            builder.Append(ReadTypeToStringName(text, ref index));
                            var depth = 0;
                            while (index < text.Length && (depth > 0 || text[index] != ']'))
                            {
                                depth += text[index] == '[' ? 1 : text[index] == ']' ? -1 : 0;
                                index++;
                            }

                            index++;
                            if (index < text.Length && text[index] == ',')
                            {
                                builder.Append(',');
                                index++;
                            }
                        }

                        builder.Append(']');
                        index++;
                    }
                    else
                    {
                        // Array ranks ("[]", "[,]") and generic arguments written without assemblies.
                        var depth = 0;
                        do
                        {
                            depth += text[index] == '[' ? 1 : text[index] == ']' ? -1 : 0;
                            builder.Append(text[index++]);
                        }
                        while (index < text.Length && depth > 0);
                    }
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Gets the name a trimmer descriptor roots a type with, with '/' between nested types. Descriptors only name type
        /// definitions: closed generics and arrays don't resolve (IL2008), and rooting their definitions instead would preserve
        /// every member of framework types (List`1, Int32...), so they aren't rooted. Their uses in the registry keep them.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The descriptor type name, or null when the type isn't rooted by name.</returns>
        internal static string? GetTrimmerDescriptorTypeName(string typeName)
        {
            var trimmedTypeName = typeName.Trim();
            if (trimmedTypeName.IndexOf('[') >= 0 || trimmedTypeName.EndsWith("*", StringComparison.Ordinal) || trimmedTypeName.EndsWith("&", StringComparison.Ordinal))
            {
                return null;
            }

            return trimmedTypeName.Replace('+', '/');
        }

        /// <summary>
        /// Parses a potentially assembly-qualified type name into type and assembly components.
        /// </summary>
        /// <param name="value">The raw type reference value.</param>
        /// <returns>The parsed type name and optional assembly name.</returns>
        internal static (string TypeName, string? AssemblyName) ParseTypeAndAssembly(string value)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(value)) evaluates to true.
            if (string.IsNullOrWhiteSpace(value))
            {
                return (string.Empty, null);
            }

            var commaIndex = FindTopLevelComma(value);
            // Branch: take this path when (commaIndex < 0) evaluates to true.
            if (commaIndex < 0)
            {
                return (value.Trim(), null);
            }

            return (value.Substring(0, commaIndex).Trim(), NormalizeAssemblyName(value.Substring(commaIndex + 1)));
        }

        /// <summary>
        /// Determines whether is generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsGenericTypeName(string typeName)
        {
            return !string.IsNullOrWhiteSpace(typeName) && typeName.IndexOf('`') >= 0;
        }

        /// <summary>
        /// Determines whether is open generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsOpenGenericTypeName(string typeName)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(typeName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            // Branch: take this path when (typeName.IndexOf('!') >= 0) evaluates to true.
            if (typeName.IndexOf('!') >= 0)
            {
                return true;
            }

            // Branch: take this path when (typeName.IndexOf('`') < 0) evaluates to true.
            if (typeName.IndexOf('`') < 0)
            {
                return false;
            }

            var genericArgumentsStart = typeName.IndexOf("[[", StringComparison.Ordinal);
            // Branch: take this path when (genericArgumentsStart < 0) evaluates to true.
            if (genericArgumentsStart < 0)
            {
                return true;
            }

            var declaredArity = CountDeclaredGenericArity(typeName, genericArgumentsStart);
            // Branch: take this path when (declaredArity <= 0) evaluates to true.
            if (declaredArity <= 0)
            {
                return false;
            }

            var providedArguments = CountTopLevelGenericArguments(typeName, genericArgumentsStart);
            return providedArguments < declaredArity;
        }

        /// <summary>
        /// Determines whether a reflection type name is an array type name ("System.Int32[]", "Foo[,]", "Box`1[[...]][]").
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the type name ends with an array rank specifier; otherwise, false.</returns>
        internal static bool IsArrayTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var trimmedTypeName = typeName.Trim();
            var rankStart = trimmedTypeName.LastIndexOf('[');
            if (rankStart <= 0 || trimmedTypeName[trimmedTypeName.Length - 1] != ']')
            {
                return false;
            }

            for (var i = rankStart + 1; i < trimmedTypeName.Length - 1; i++)
            {
                if (trimmedTypeName[i] != ',' && trimmedTypeName[i] != '*' && trimmedTypeName[i] != ' ')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether is closed generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsClosedGenericTypeName(string typeName)
        {
            return IsGenericTypeName(typeName) && !IsOpenGenericTypeName(typeName);
        }

        /// <summary>
        /// Gets the generic arity declared by a reflection type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The declared generic arity.</returns>
        internal static int GetDeclaredGenericArity(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return 0;
            }

            var genericArgumentsStart = typeName.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentsStart < 0)
            {
                genericArgumentsStart = typeName.Length;
            }

            return CountDeclaredGenericArity(typeName, genericArgumentsStart);
        }

        /// <summary>
        /// Attempts to split a closed generic reflection type name into its generic definition and argument suffix.
        /// </summary>
        /// <param name="typeName">The closed generic type name value.</param>
        /// <param name="genericTypeDefinitionName">The generic type definition name.</param>
        /// <param name="genericArgumentsSuffix">The generic arguments suffix, including brackets.</param>
        /// <param name="genericArgumentCount">The generic argument count.</param>
        /// <returns>true when the type name is a closed generic type name; otherwise, false.</returns>
        internal static bool TrySplitClosedGenericTypeName(
            string typeName,
            out string genericTypeDefinitionName,
            out string genericArgumentsSuffix,
            out int genericArgumentCount)
        {
            genericTypeDefinitionName = string.Empty;
            genericArgumentsSuffix = string.Empty;
            genericArgumentCount = 0;

            if (!IsClosedGenericTypeName(typeName))
            {
                return false;
            }

            var genericArgumentsStart = typeName.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentsStart < 0)
            {
                return false;
            }

            genericTypeDefinitionName = typeName.Substring(0, genericArgumentsStart);
            genericArgumentsSuffix = typeName.Substring(genericArgumentsStart);
            genericArgumentCount = CountTopLevelGenericArguments(typeName, genericArgumentsStart);
            return genericArgumentCount > 0;
        }

        private static bool TryCanonicalizeTypeName(string value, ref int index, out string canonicalTypeName)
        {
            canonicalTypeName = string.Empty;
            var nameStart = index;
            while (index < value.Length && value[index] != '[' && value[index] != ']' && value[index] != ',')
            {
                index++;
            }

            var builder = new StringBuilder(value.Substring(nameStart, index - nameStart).Trim().Replace('/', '+'));
            if (builder.Length == 0)
            {
                return false;
            }

            // Generic arguments: "[[Type, Assembly...],[Type, Assembly...]]", or unqualified "[Type,Type]".
            if (index + 1 < value.Length && value[index] == '[' && value[index + 1] != ']' && value[index + 1] != ',' && value[index + 1] != '*')
            {
                index++;
                var arguments = new List<string>();
                while (true)
                {
                    SkipSpaces(value, ref index);
                    if (index >= value.Length)
                    {
                        return false;
                    }

                    string argument;
                    if (value[index] == '[')
                    {
                        index++;
                        if (!TryCanonicalizeTypeName(value, ref index, out var argumentTypeName))
                        {
                            return false;
                        }

                        SkipSpaces(value, ref index);
                        var argumentAssemblyName = string.Empty;
                        if (index < value.Length && value[index] == ',')
                        {
                            // Keep the simple assembly name only, and skip Version/Culture/PublicKeyToken.
                            var assemblyStart = ++index;
                            while (index < value.Length && value[index] != ']')
                            {
                                index++;
                            }

                            argumentAssemblyName = NormalizeAssemblyName(value.Substring(assemblyStart, index - assemblyStart));
                        }

                        if (index >= value.Length || value[index] != ']')
                        {
                            return false;
                        }

                        index++;
                        argument = argumentAssemblyName.Length == 0 ? $"[{argumentTypeName}]" : $"[{argumentTypeName}, {argumentAssemblyName}]";
                    }
                    else if (!TryCanonicalizeTypeName(value, ref index, out argument))
                    {
                        return false;
                    }

                    arguments.Add(argument);
                    SkipSpaces(value, ref index);
                    if (index >= value.Length)
                    {
                        return false;
                    }

                    if (value[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    if (value[index] != ']')
                    {
                        return false;
                    }

                    index++;
                    break;
                }

                builder.Append('[').Append(string.Join(",", arguments)).Append(']');
            }

            // Array suffixes ("[]", "[,]", "[*]") are kept as they are.
            while (index < value.Length && value[index] == '[')
            {
                var suffixStart = index;
                while (index < value.Length && value[index] != ']')
                {
                    index++;
                }

                if (index >= value.Length)
                {
                    return false;
                }

                index++;
                builder.Append(value, suffixStart, index - suffixStart);
            }

            canonicalTypeName = builder.ToString();
            return true;
        }

        private static void SkipSpaces(string value, ref int index)
        {
            while (index < value.Length && value[index] == ' ')
            {
                index++;
            }
        }

        /// <summary>
        /// Executes find top level comma.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int FindTopLevelComma(string value)
        {
            var bracketDepth = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                // Branch: take this path when (c == '[') evaluates to true.
                if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    // Branch: take this path when (c == ']') evaluates to true.
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                }
                else if (c == ',' && bracketDepth == 0)
                {
                    // Branch: take this path when (c == ',' && bracketDepth == 0) evaluates to true.
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Executes count declared generic arity.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="genericArgumentsStart">The generic arguments start value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int CountDeclaredGenericArity(string typeName, int genericArgumentsStart)
        {
            var arity = 0;
            for (var i = 0; i < genericArgumentsStart; i++)
            {
                // Branch: take this path when (typeName[i] != '`') evaluates to true.
                if (typeName[i] != '`')
                {
                    continue;
                }

                var digitsStart = i + 1;
                // Branch: take this path when (digitsStart >= genericArgumentsStart || !char.IsDigit(typeName[digitsStart])) evaluates to true.
                if (digitsStart >= genericArgumentsStart || !char.IsDigit(typeName[digitsStart]))
                {
                    continue;
                }

                var digitsEnd = digitsStart;
                while (digitsEnd < genericArgumentsStart && char.IsDigit(typeName[digitsEnd]))
                {
                    digitsEnd++;
                }

                // Branch: take this path when (int.TryParse(typeName.Substring(digitsStart, digitsEnd - digitsStart), out var parsedArity)) evaluates to true.
                if (int.TryParse(typeName.Substring(digitsStart, digitsEnd - digitsStart), out var parsedArity))
                {
                    arity += parsedArity;
                }

                i = digitsEnd - 1;
            }

            return arity;
        }

        /// <summary>
        /// Executes count top level generic arguments.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="genericArgumentsStart">The generic arguments start value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int CountTopLevelGenericArguments(string typeName, int genericArgumentsStart)
        {
            var bracketDepth = 0;
            var argumentCount = 0;
            var hasStartedRootArgumentList = false;

            for (var i = genericArgumentsStart; i < typeName.Length; i++)
            {
                var current = typeName[i];
                // Branch: take this path when (current == '[') evaluates to true.
                if (current == '[')
                {
                    bracketDepth++;
                    // Branch: take this path when (bracketDepth == 2 && !hasStartedRootArgumentList) evaluates to true.
                    if (bracketDepth == 2 && !hasStartedRootArgumentList)
                    {
                        hasStartedRootArgumentList = true;
                        argumentCount = 1;
                    }

                    continue;
                }

                // Branch: take this path when (current == ']') evaluates to true.
                if (current == ']')
                {
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    // Branch: take this path when (hasStartedRootArgumentList && bracketDepth == 0) evaluates to true.
                    if (hasStartedRootArgumentList && bracketDepth == 0)
                    {
                        break;
                    }

                    continue;
                }

                // Branch: take this path when (current == ',' && hasStartedRootArgumentList && bracketDepth == 1) evaluates to true.
                if (current == ',' && hasStartedRootArgumentList && bracketDepth == 1)
                {
                    argumentCount++;
                }
            }

            return argumentCount;
        }
    }
}
