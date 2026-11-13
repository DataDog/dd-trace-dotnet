// <copyright file="ProfileLabels.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class ProfileLabels : IReadOnlyList<ProfileLabel>
    {
        private readonly string _origin;
        private readonly ProfileLabel[] _labels;

        internal ProfileLabels(string origin, ProfileLabel[] labels)
        {
            _origin = origin;
            _labels = labels;
        }

        public int Count => _labels.Length;

        public ProfileLabel this[int index] => _labels[index];

        public bool Has(string name) => GetValues(name).Count > 0;

        public string GetRequired(string name)
        {
            var values = GetValues(name);
            if (values.Count == 0)
            {
                throw new InvalidOperationException(
                    "Profile " + _origin + " has no label '" + name + "'. Labels: " + this + ".");
            }

            if (values.Count > 1)
            {
                throw new InvalidOperationException(
                    "Profile " + _origin + " has " + values.Count + " values for label '" + name + "': " + string.Join(", ", values) + ".");
            }

            return values[0];
        }

        public IReadOnlyList<string> GetValues(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var values = new List<string>();
            foreach (var label in _labels)
            {
                if (string.Equals(label.Name, name, StringComparison.Ordinal))
                {
                    values.Add(label.Value);
                }
            }

            return values;
        }

        public IEnumerator<ProfileLabel> GetEnumerator() => ((IEnumerable<ProfileLabel>)_labels).GetEnumerator();

        public override string ToString()
        {
            if (_labels.Length == 0)
            {
                return "(none)";
            }

            var parts = new string[_labels.Length];
            for (var i = 0; i < _labels.Length; i++)
            {
                parts[i] = _labels[i].ToString();
            }

            return string.Join(", ", parts);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
