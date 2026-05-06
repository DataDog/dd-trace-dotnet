// <copyright file="DuckTypeAotGenericInstantiationsParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot generic instantiations parser.
    /// </summary>
    internal static class DuckTypeAotGenericInstantiationsParser
    {
        /// <summary>
        /// Parses parse.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotGenericInstantiationsParseResult Parse(string path)
        {
            var errors = new List<string>();
            var typeRoots = new Dictionary<string, DuckTypeAotTypeReference>(StringComparer.Ordinal);

            // Branch: take this path when (string.IsNullOrWhiteSpace(path)) evaluates to true.
            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotGenericInstantiationsParseResult(Array.Empty<DuckTypeAotTypeReference>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var rootToken = JsonConvert.DeserializeObject<JToken>(json);
                // Branch: take this path when (rootToken is null) evaluates to true.
                if (rootToken is null)
                {
                    errors.Add($"--generic-instantiations content is empty or invalid JSON: {path}");
                    return new DuckTypeAotGenericInstantiationsParseResult(Array.Empty<DuckTypeAotTypeReference>(), errors);
                }

                var entries = rootToken switch
                {
                    JArray rootArray => rootArray,
                    JObject rootObject when rootObject["instantiations"] is JArray instantiations => instantiations,
                    _ => null
                };

                // Branch: take this path when (entries is null) evaluates to true.
                if (entries is null)
                {
                    errors.Add($"--generic-instantiations must be a JSON array or a JSON object containing an 'instantiations' array: {path}");
                    return new DuckTypeAotGenericInstantiationsParseResult(Array.Empty<DuckTypeAotTypeReference>(), errors);
                }

                ParseEntries(entries, path, typeRoots, errors);
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                errors.Add($"--generic-instantiations could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotGenericInstantiationsParseResult(typeRoots.Values, errors);
        }

        /// <summary>
        /// Parses parse entries.
        /// </summary>
        /// <param name="entries">The entries value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="typeRoots">The type roots value.</param>
        /// <param name="errors">The errors value.</param>
        private static void ParseEntries(
            JArray entries,
            string path,
            IDictionary<string, DuckTypeAotTypeReference> typeRoots,
            ICollection<string> errors)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                // Branch: take this path when (!TryParseEntry(entries[i], path, i, errors, out var typeRoot)) evaluates to true.
                if (!TryParseEntry(entries[i], path, i, errors, out var typeRoot))
                {
                    continue;
                }

                typeRoots[typeRoot.Key] = typeRoot;
            }
        }

        /// <summary>
        /// Attempts to try parse entry.
        /// </summary>
        /// <param name="token">The token value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="typeRoot">The type root value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryParseEntry(
            JToken token,
            string path,
            int index,
            ICollection<string> errors,
            out DuckTypeAotTypeReference typeRoot)
        {
            typeRoot = null!;

            string? typeName;
            string? assemblyName;
            // Branch dispatch: select the execution path based on (token.Type).
            switch (token.Type)
            {
                case JTokenType.String:
                    // Branch: handles the case JTokenType.String switch case.
                {
                    var rawTypeAndAssembly = token.Value<string>() ?? string.Empty;
                    var parsed = DuckTypeAotNameHelpers.ParseTypeAndAssembly(rawTypeAndAssembly);
                    typeName = parsed.TypeName;
                    assemblyName = parsed.AssemblyName;
                    break;
                }

                case JTokenType.Object:
                    // Branch: handles the case JTokenType.Object switch case.
                {
                    var entry = (JObject)token;
                    var rawTypeAndAssembly = entry["type"]?.ToString() ?? string.Empty;
                    var parsed = DuckTypeAotNameHelpers.ParseTypeAndAssembly(rawTypeAndAssembly);
                    typeName = parsed.TypeName;
                    assemblyName = entry["assembly"]?.ToString() ?? parsed.AssemblyName;
                    break;
                }

                default:
                    // Branch: fallback switch case when no explicit case label matches.
                    errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must be a string or object.");
                    return false;
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(assemblyName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide type and assembly.");
                return false;
            }

            assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName!);
            // Branch: take this path when (!DuckTypeAotNameHelpers.IsGenericTypeName(typeName)) evaluates to true.
            if (!DuckTypeAotNameHelpers.IsGenericTypeName(typeName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide a closed generic type. '{typeName}' is not generic.");
                return false;
            }

            // Branch: take this path when (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName)) evaluates to true.
            if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide a closed generic type. '{typeName}' is open.");
                return false;
            }

            typeRoot = new DuckTypeAotTypeReference(typeName, assemblyName);
            return true;
        }
    }

    /// <summary>
    /// Represents duck type aot type reference.
    /// </summary>
    internal sealed class DuckTypeAotTypeReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotTypeReference"/> class.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="assemblyName">The assembly name value.</param>
        public DuckTypeAotTypeReference(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
        }

        /// <summary>
        /// Gets type name.
        /// </summary>
        /// <value>The type name value.</value>
        public string TypeName { get; }

        /// <summary>
        /// Gets assembly name.
        /// </summary>
        /// <value>The assembly name value.</value>
        public string AssemblyName { get; }

        public string Key =>
            string.Concat(
                AssemblyName.ToUpperInvariant(),
                "|",
                TypeName);
    }

    /// <summary>
    /// Represents duck type aot generic instantiations parse result.
    /// </summary>
    internal sealed class DuckTypeAotGenericInstantiationsParseResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotGenericInstantiationsParseResult"/> class.
        /// </summary>
        /// <param name="typeRoots">The type roots value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotGenericInstantiationsParseResult(IEnumerable<DuckTypeAotTypeReference> typeRoots, IReadOnlyList<string> errors)
        {
            TypeRoots = new List<DuckTypeAotTypeReference>(typeRoots);
            Errors = errors;
        }

        /// <summary>
        /// Gets type roots.
        /// </summary>
        /// <value>The type roots value.</value>
        public IReadOnlyList<DuckTypeAotTypeReference> TypeRoots { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }
}
