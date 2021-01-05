using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Attribute that indicates that the decorated class is meant to intercept a method
    /// by modifying the method body with callbacks. Used to generate the integration definitions file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class InstrumentMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the assembly that contains the target method to be intercepted.
        /// Required if <see cref="Assemblies"/> is not set.
        /// </summary>
        public string Assembly
        {
            get => string.Empty;
            set => Assemblies = new[] { value };
        }

        /// <summary>
        /// Gets or sets the name of the assemblies that contain the target method to be intercepted.
        /// Required if <see cref="Assembly"/> is not set.
        /// </summary>
        public string[] Assemblies { get; set; }

        /// <summary>
        /// Gets or sets the name of the type that contains the target method to be intercepted.
        /// Required.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the target method to be intercepted.
        /// If null, default to the name of the decorated method.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the return type name
        /// </summary>
        public string ReturnTypeName { get; set; }

        /// <summary>
        /// Gets or sets the parameters type array for the target method to be intercepted.
        /// </summary>
        public string[] ParametersTypesNames { get; set; }

        /// <summary>
        /// Gets the target version range for <see cref="Assembly"/>.
        /// </summary>
        public IntegrationVersionRange VersionRange { get; } = new IntegrationVersionRange();

        /// <summary>
        /// Gets or sets the target minimum version.
        /// </summary>
        public string MinimumVersion
        {
            get => VersionRange.MinimumVersion;
            set => VersionRange.MinimumVersion = value;
        }

        /// <summary>
        /// Gets or sets the target maximum version.
        /// </summary>
        public string MaximumVersion
        {
            get => VersionRange.MaximumVersion;
            set => VersionRange.MaximumVersion = value;
        }

        /// <summary>
        /// Gets or sets the integration name. Allows to group several integration with a single integration name.
        /// </summary>
        public string IntegrationName { get; set; }
    }
}
