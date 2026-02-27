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
    internal static class DuckTypeAotGenericInstantiationsParser
    {
        internal static DuckTypeAotGenericInstantiationsParseResult Parse(string path)
        {
            var errors = new List<string>();
            var typeRoots = new Dictionary<string, DuckTypeAotTypeReference>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotGenericInstantiationsParseResult(Array.Empty<DuckTypeAotTypeReference>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var rootToken = JsonConvert.DeserializeObject<JToken>(json);
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

                if (entries is null)
                {
                    errors.Add($"--generic-instantiations must be a JSON array or a JSON object containing an 'instantiations' array: {path}");
                    return new DuckTypeAotGenericInstantiationsParseResult(Array.Empty<DuckTypeAotTypeReference>(), errors);
                }

                ParseEntries(entries, path, typeRoots, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"--generic-instantiations could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotGenericInstantiationsParseResult(typeRoots.Values, errors);
        }

        private static void ParseEntries(
            JArray entries,
            string path,
            IDictionary<string, DuckTypeAotTypeReference> typeRoots,
            ICollection<string> errors)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryParseEntry(entries[i], path, i, errors, out var typeRoot))
                {
                    continue;
                }

                typeRoots[typeRoot.Key] = typeRoot;
            }
        }

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
            switch (token.Type)
            {
                case JTokenType.String:
                {
                    var rawTypeAndAssembly = token.Value<string>() ?? string.Empty;
                    var parsed = DuckTypeAotNameHelpers.ParseTypeAndAssembly(rawTypeAndAssembly);
                    typeName = parsed.TypeName;
                    assemblyName = parsed.AssemblyName;
                    break;
                }

                case JTokenType.Object:
                {
                    var entry = (JObject)token;
                    var rawTypeAndAssembly = entry["type"]?.ToString() ?? string.Empty;
                    var parsed = DuckTypeAotNameHelpers.ParseTypeAndAssembly(rawTypeAndAssembly);
                    typeName = parsed.TypeName;
                    assemblyName = entry["assembly"]?.ToString() ?? parsed.AssemblyName;
                    break;
                }

                default:
                    errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must be a string or object.");
                    return false;
            }

            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(assemblyName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide type and assembly.");
                return false;
            }

            assemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName!);
            if (!DuckTypeAotNameHelpers.IsGenericTypeName(typeName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide a closed generic type. '{typeName}' is not generic.");
                return false;
            }

            if (!DuckTypeAotNameHelpers.IsClosedGenericTypeName(typeName))
            {
                errors.Add($"--generic-instantiations entry #{index + 1} in '{path}' must provide a closed generic type. '{typeName}' is open.");
                return false;
            }

            typeRoot = new DuckTypeAotTypeReference(typeName, assemblyName);
            return true;
        }
    }

    internal sealed class DuckTypeAotTypeReference
    {
        public DuckTypeAotTypeReference(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName);
        }

        public string TypeName { get; }

        public string AssemblyName { get; }

        public string Key =>
            string.Concat(
                AssemblyName.ToUpperInvariant(),
                "|",
                TypeName);
    }

    internal sealed class DuckTypeAotGenericInstantiationsParseResult
    {
        public DuckTypeAotGenericInstantiationsParseResult(IEnumerable<DuckTypeAotTypeReference> typeRoots, IReadOnlyList<string> errors)
        {
            TypeRoots = new List<DuckTypeAotTypeReference>(typeRoots);
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotTypeReference> TypeRoots { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
