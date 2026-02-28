// <copyright file="DuckTypeAotScenarioInventoryParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot scenario inventory parser.
    /// </summary>
    internal static class DuckTypeAotScenarioInventoryParser
    {
        /// <summary>
        /// Parses parse.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotScenarioInventoryParseResult Parse(string path)
        {
            var errors = new List<string>();
            var requiredScenarios = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotScenarioInventoryParseResult(Array.Empty<string>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<ScenarioInventoryDocument>(json);
                if (parsedFile is null)
                {
                    errors.Add($"--scenario-inventory content is empty or invalid JSON: {path}");
                    return new DuckTypeAotScenarioInventoryParseResult(Array.Empty<string>(), errors);
                }

                var hasAnyEntry = false;
                ParseEntries(parsedFile.RequiredScenarios, path, errors, requiredScenarios, ref hasAnyEntry);
                ParseEntries(parsedFile.RequiredScenarioIds, path, errors, requiredScenarios, ref hasAnyEntry);
                if (!hasAnyEntry)
                {
                    errors.Add($"--scenario-inventory does not contain any entries: {path}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"--scenario-inventory could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotScenarioInventoryParseResult(requiredScenarios, errors);
        }

        /// <summary>
        /// Parses parse entries.
        /// </summary>
        /// <param name="entries">The entries value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="requiredScenarios">The required scenarios value.</param>
        /// <param name="hasAnyEntry">The has any entry value.</param>
        private static void ParseEntries(
            IReadOnlyList<string>? entries,
            string path,
            ICollection<string> errors,
            ISet<string> requiredScenarios,
            ref bool hasAnyEntry)
        {
            if (entries is null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                hasAnyEntry = true;
                var rawEntry = entries[i];
                if (string.IsNullOrWhiteSpace(rawEntry))
                {
                    errors.Add($"--scenario-inventory entry #{i + 1} in '{path}' is empty.");
                    continue;
                }

                var entry = rawEntry.Trim();
                if (!IsValidScenarioEntry(entry))
                {
                    errors.Add(
                        $"--scenario-inventory entry #{i + 1} in '{path}' is invalid: '{entry}'. " +
                        "Use exact IDs (for example 'A-01') or a single trailing wildcard (for example 'FG-*').");
                    continue;
                }

                if (!requiredScenarios.Add(entry))
                {
                    errors.Add($"--scenario-inventory contains duplicate entry '{entry}' in '{path}'.");
                }
            }
        }

        /// <summary>
        /// Determines whether is valid scenario entry.
        /// </summary>
        /// <param name="entry">The entry value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsValidScenarioEntry(string entry)
        {
            var wildcardIndex = entry.IndexOf('*');
            if (wildcardIndex < 0)
            {
                return true;
            }

            return wildcardIndex == entry.Length - 1 && entry.LastIndexOf('*') == wildcardIndex;
        }

        /// <summary>
        /// Represents scenario inventory document.
        /// </summary>
        private sealed class ScenarioInventoryDocument
        {
            /// <summary>
            /// Gets or sets required scenarios.
            /// </summary>
            /// <value>The required scenarios value.</value>
            [JsonProperty("requiredScenarios")]
            public List<string>? RequiredScenarios { get; set; }

            /// <summary>
            /// Gets or sets required scenario ids.
            /// </summary>
            /// <value>The required scenario ids value.</value>
            [JsonProperty("requiredScenarioIds")]
            public List<string>? RequiredScenarioIds { get; set; }
        }
    }
}
