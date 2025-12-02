// <copyright file="DisableActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Activity.Handlers
{
    internal sealed class DisableActivityHandler : IActivityHandler
    {
        private List<Regex>? _disabledSourceNameGlobs = null;
        private bool _disableAll = false;

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            // do nothing; this should not be called
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // do nothing; this should not be called
        }

        /// <summary>
        /// Determines whether <see cref="DisableActivityHandler"/> will "listen" to <paramref name="sourceName"/>.
        /// <para>
        /// Note that "listen" in this case means that the created ActivityListener will not subscribe to the ActivitySource.
        /// </para>
        /// </summary>
        /// <returns><see langword="true"/> when the Tracer will disable the ActivitySource; otherwise <see langword="false"/></returns>
        public bool ShouldListenTo(string sourceName, string? version)
        {
            if (_disableAll)
            {
                return true; // "*" was specified as a pattern, short circuit to disable all
            }

            _disabledSourceNameGlobs ??= PopulateGlobs();
            if (_disabledSourceNameGlobs.Count == 0)
            {
                return false; // no glob patterns specified, sourceName will not be disabled
            }

            foreach (var regex in _disabledSourceNameGlobs)
            {
                if (regex.IsMatch(sourceName))
                {
                    return true; // disable ActivitySource of "sourceName" from being listened to by the tracer
                }
            }

            // sources were specified to be disabled, but this sourceName didn't match any of them
            return false; // sourceName will _not_ be disabled
        }

        private List<Regex> PopulateGlobs()
        {
            var globs = new List<Regex>();
            var toDisable = Tracer.Instance.Settings.DisabledActivitySources;
            if (toDisable is null || toDisable.Length == 0)
            {
                return globs;
            }

            foreach (var disabledSourceNameGlob in toDisable)
            {
                // HACK: using RegexBuilder here even though it isn't _really_ for this
                var globRegex = RegexBuilder.Build(disabledSourceNameGlob, SamplingRulesFormat.Glob, RegexBuilder.DefaultTimeout);
                // handle special case where a "*" pattern will be null
                if (globRegex is null)
                {
                    _disableAll = true;
                    return [];
                }

                globs.Add(globRegex);
            }

            return globs;
        }
    }
}
