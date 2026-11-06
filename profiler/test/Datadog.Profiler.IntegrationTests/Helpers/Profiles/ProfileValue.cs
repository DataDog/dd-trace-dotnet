// <copyright file="ProfileValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    /// <summary>
    /// A sample-type name and unit. The static members are aliases for the types the profiler emits.
    /// Lookup is by name, so an unlisted type is read with <see cref="Named"/>.
    /// </summary>
    internal readonly struct ProfileValue : IEquatable<ProfileValue>
    {
        private ProfileValue(string name, string unit)
        {
            Name = name;
            Unit = unit;
        }

        public static ProfileValue CpuTime { get; } = new ProfileValue("cpu", "nanoseconds");

        public static ProfileValue CpuSampleCount { get; } = new ProfileValue("cpu-samples", "count");

        public static ProfileValue WallTime { get; } = new ProfileValue("wall", "nanoseconds");

        public static ProfileValue AllocationCount { get; } = new ProfileValue("alloc-samples", "count");

        public static ProfileValue AllocationSize { get; } = new ProfileValue("alloc-size", "bytes");

        public static ProfileValue ExceptionCount { get; } = new ProfileValue("exception", "count");

        public static ProfileValue LockCount { get; } = new ProfileValue("lock-count", "count");

        public static ProfileValue LockTime { get; } = new ProfileValue("lock-time", "nanoseconds");

        public static ProfileValue Timeline { get; } = new ProfileValue("timeline", "nanoseconds");

        public static ProfileValue RequestTime { get; } = new ProfileValue("request-time", "nanoseconds");

        public static ProfileValue LiveObjectCount { get; } = new ProfileValue("inuse-objects", "count");

        public static ProfileValue LiveObjectSpace { get; } = new ProfileValue("inuse-space", "bytes");

        public string Name { get; }

        public string Unit { get; }

        public static bool operator ==(ProfileValue left, ProfileValue right) => left.Equals(right);

        public static bool operator !=(ProfileValue left, ProfileValue right) => !left.Equals(right);

        public static ProfileValue Named(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return new ProfileValue(name, null);
        }

        public bool Equals(ProfileValue other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Unit, other.Unit, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ProfileValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;
                hash = (hash * 397) ^ (Unit != null ? StringComparer.Ordinal.GetHashCode(Unit) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return Unit == null ? Name : Name + " (" + Unit + ")";
        }

        internal static ProfileValue FromSampleType(string name, string unit) => new ProfileValue(name, unit);
    }
}
