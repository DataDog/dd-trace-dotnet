// <copyright file="IntegrationVersionRange.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Specifies a safe version range for an integration.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IntegrationVersionRange
    {
        /// <summary>
        /// Gets the minimum major version.
        /// </summary>
        public ushort MinimumMajor { get; private set; } = ushort.MinValue;

        /// <summary>
        /// Gets the minimum minor version.
        /// </summary>
        public ushort MinimumMinor { get; private set; } = ushort.MinValue;

        /// <summary>
        /// Gets the minimum patch version.
        /// </summary>
        public ushort MinimumPatch { get; private set; } = ushort.MinValue;

        /// <summary>
        /// Gets the maximum major version.
        /// </summary>
        public ushort MaximumMajor { get; private set; } = ushort.MaxValue;

        /// <summary>
        /// Gets the maximum minor version.
        /// </summary>
        public ushort MaximumMinor { get; private set; } = ushort.MaxValue;

        /// <summary>
        /// Gets the maximum patch version.
        /// </summary>
        public ushort MaximumPatch { get; private set; } = ushort.MaxValue;

        /// <summary>
        /// Gets or sets the MinimumMajor, MinimumMinor, and MinimumPatch properties.
        /// Convenience property for setting target minimum version.
        /// </summary>
        public string MinimumVersion
        {
            get => $"{MinimumMajor}.{MinimumMinor}.{MinimumPatch}";
            internal set
            {
                MinimumMajor = MinimumMinor = MinimumPatch = ushort.MinValue;
                var parts = value.Split('.');
                if (parts.Length > 0 && parts[0] != "*")
                {
                    MinimumMajor = ushort.Parse(parts[0]);
                }

                if (parts.Length > 1 && parts[1] != "*")
                {
                    MinimumMinor = ushort.Parse(parts[1]);
                }

                if (parts.Length > 2 && parts[2] != "*")
                {
                    MinimumPatch = ushort.Parse(parts[2]);
                }
            }
        }

        /// <summary>
        /// Gets or sets the MaximumMajor, MaximumMinor, and MaximumPatch properties.
        /// Convenience property for setting target maximum version.
        /// </summary>
        public string MaximumVersion
        {
            get => $"{MaximumMajor}.{MaximumMinor}.{MaximumPatch}";
            internal set
            {
                MaximumMajor = MaximumMinor = MaximumPatch = ushort.MaxValue;
                var parts = value.Split('.');
                if (parts.Length > 0 && parts[0] != "*")
                {
                    MaximumMajor = ushort.Parse(parts[0]);
                }

                if (parts.Length > 1 && parts[1] != "*")
                {
                    MaximumMinor = ushort.Parse(parts[1]);
                }

                if (parts.Length > 2 && parts[2] != "*")
                {
                    MaximumPatch = ushort.Parse(parts[2]);
                }
            }
        }
    }
}
