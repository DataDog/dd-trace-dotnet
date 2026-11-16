// <copyright file="ProfileLabel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal readonly struct ProfileLabel
    {
        private ProfileLabel(string name, string value, bool isNumeric, long number, string unit)
        {
            Name = name;
            Value = value;
            IsNumeric = isNumeric;
            Number = number;
            Unit = unit;
        }

        public string Name { get; }

        public string Value { get; }

        public bool IsNumeric { get; }

        public long Number { get; }

        public string Unit { get; }

        public static ProfileLabel Text(string name, string value)
        {
            return new ProfileLabel(name, value, false, 0, string.Empty);
        }

        public static ProfileLabel Numeric(string name, long number, string unit)
        {
            return new ProfileLabel(name, number.ToString(), true, number, unit ?? string.Empty);
        }

        public override string ToString()
        {
            if (!IsNumeric)
            {
                return Name + "=" + Value;
            }

            return Unit.Length == 0 ? Name + "=" + Value : Name + "=" + Value + " " + Unit;
        }
    }
}
