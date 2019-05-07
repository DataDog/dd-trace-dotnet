namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Specifies a safe version range for an integration.
    /// </summary>
    public class IntegrationVersionRange
    {
        /// <summary>
        /// Gets or sets the minimum major version.
        /// </summary>
        public ushort MinimumMajor { get; set; } = ushort.MinValue;

        /// <summary>
        /// Gets or sets the minimum minor version.
        /// </summary>
        public ushort MinimumMinor { get; set; } = ushort.MinValue;

        /// <summary>
        /// Gets or sets the minimum patch version.
        /// </summary>
        public ushort MinimumPatch { get; set; } = ushort.MinValue;

        /// <summary>
        /// Gets or sets the maximum major version.
        /// </summary>
        public ushort MaximumMajor { get; set; } = ushort.MaxValue;

        /// <summary>
        /// Gets or sets the maximum minor version.
        /// </summary>
        public ushort MaximumMinor { get; set; } = ushort.MaxValue;

        /// <summary>
        /// Gets or sets the maximum patch version.
        /// </summary>
        public ushort MaximumPatch { get; set; } = ushort.MaxValue;

        /// <summary>
        /// Gets or sets the MinimumMajor, MinimumMinor, and MinimumPatch properties.
        /// Convenience property for setting target minimum version.
        /// </summary>
        public string MinimumVersion
        {
            get => $"{MinimumMajor}.{MinimumMinor}.{MinimumPatch}";
            set
            {
                var parts = value.Split('.');
                if (parts.Length > 0)
                {
                    MinimumMajor = ushort.Parse(parts[0]);
                }

                if (parts.Length > 1)
                {
                    MinimumMinor = ushort.Parse(parts[1]);
                }

                if (parts.Length > 2)
                {
                    MinimumPatch = ushort.Parse(parts[2]);
                }
            }
        }

        /// <summary>
        /// Gets or sets the MaximumMajor, MaximumMinor, and MaximumPatch properties.
        /// Convenience property for setting target minimum version.
        /// </summary>
        public string MaximumVersion
        {
            get => $"{MaximumMajor}.{MaximumMinor}.{MaximumPatch}";
            set
            {
                var parts = value.Split('.');
                if (parts.Length > 0)
                {
                    MaximumMajor = ushort.Parse(parts[0]);
                }

                if (parts.Length > 1)
                {
                    MaximumMinor = ushort.Parse(parts[1]);
                }

                if (parts.Length > 2)
                {
                    MaximumPatch = ushort.Parse(parts[2]);
                }
            }
        }
    }
}
