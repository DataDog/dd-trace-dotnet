namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Specifies a safe version range for an integration.
    /// </summary>
    public class IntegrationVersionRange
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
        /// Gets the MinimumMajor, MinimumMinor, and MinimumPatch properties.
        /// Convenience property for setting target minimum version.
        /// </summary>
        public string MinimumVersion
        {
            get => $"{MinimumMajor}.{MinimumMinor}.{MinimumPatch}";
            internal set
            {
                MinimumMajor = MinimumMinor = MinimumPatch = ushort.MinValue;
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
        /// Gets the MaximumMajor, MaximumMinor, and MaximumPatch properties.
        /// Convenience property for setting target minimum version.
        /// </summary>
        public string MaximumVersion
        {
            get => $"{MaximumMajor}.{MaximumMinor}.{MaximumPatch}";
            internal set
            {
                MaximumMajor = MaximumMinor = MaximumPatch = ushort.MaxValue;
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
