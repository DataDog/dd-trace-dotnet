// <copyright file="ProfileValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class ProfileValues
    {
        private readonly string _origin;
        private readonly IReadOnlyList<ProfileValue> _sampleTypes;
        private readonly bool[] _present;
        private readonly long[] _values;

        internal ProfileValues(string origin, IReadOnlyList<ProfileValue> sampleTypes, bool[] present, long[] values)
        {
            _origin = origin;
            _sampleTypes = sampleTypes;
            _present = present;
            _values = values;
        }

        public ProfileValueRead TryGetValue(ProfileValue value)
        {
            var index = IndexOf(value);
            if (index < 0)
            {
                return ProfileValueRead.Unknown;
            }

            if (!_present[index])
            {
                return ProfileValueRead.Missing;
            }

            return ProfileValueRead.Present(_values[index]);
        }

        public long GetRequired(ProfileValue value)
        {
            var read = TryGetValue(value);
            if (read.IsPresent)
            {
                return read.Value;
            }

            if (read.IsMissing)
            {
                throw new InvalidOperationException(
                    "Profile " + _origin + " is missing value '" + value.Name + "'. Available types: " + AvailableTypes() + ".");
            }

            throw new InvalidOperationException(
                "Profile " + _origin + " does not contain value type '" + value.Name + "'. Available types: " + AvailableTypes() + ".");
        }

        private int IndexOf(ProfileValue value)
        {
            if (value.Name is null)
            {
                throw new ArgumentException("A profile value needs a name.", nameof(value));
            }

            for (var i = 0; i < _sampleTypes.Count; i++)
            {
                if (string.Equals(_sampleTypes[i].Name, value.Name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private string AvailableTypes()
        {
            var parts = new string[_sampleTypes.Count];
            for (var i = 0; i < _sampleTypes.Count; i++)
            {
                parts[i] = _sampleTypes[i].ToString();
            }

            return string.Join(", ", parts);
        }
    }
}
