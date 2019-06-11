using System;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Attribute that indicates that the decorated method is meant to intercept calls
    /// to another method. Used to generate the integration definitions file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class InterceptMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the integration.
        /// </summary>
        /// <remarks>
        /// Multiple method replacements with the same integration name are grouped together.
        /// </remarks>
        public string Integration { get; set; }

        /// <summary>
        /// Gets or sets the name of the assembly where calls to the target method are searched.
        /// If null, search in all loaded assemblies.
        /// </summary>
        public string CallerAssembly { get; set; }

        /// <summary>
        /// Gets or sets the name of the type where calls to the target method are searched.
        /// If null, search in all types defined in loaded assemblies.
        /// </summary>
        public string CallerType { get; set; }

        /// <summary>
        /// Gets or sets the name of the method where calls to the target method are searched.
        /// If null, search in all loaded types.
        /// </summary>
        public string CallerMethod { get; set; }

        /// <summary>
        /// Gets or sets the name of the assembly that contains the target method to be intercepted.
        /// Required.
        /// </summary>
        public string TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets the name of the type that contains the target method to be intercepted.
        /// Required.
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// Gets or sets the name of the target method to be intercepted.
        /// If null, default to the name of the decorated method.
        /// </summary>
        public string TargetMethod { get; set; }

        /// <summary>
        /// Gets or sets the method signature that is matched to the target method to be intercepted.
        /// If null, signature check is not done.
        /// </summary>
        public string TargetSignature { get; set; }

        /// <summary>
        /// Gets or sets the explicit type array for the target method to be intercepted.
        /// This is a required field.
        /// Follows format:
        ///     new[] { return_type, param_1_type, param_2_type, ..., param_n_type }
        /// Must match the wrapper method in count:
        ///     n (parameters) + 1 (return type) - (is_instance_method : 1 : 0) - 1 (opcode)
        /// Indexes with "_" are ignored for comparison purposes.
        /// </summary>
        public string[] TargetSignatureTypes { get; set; }

        /// <summary>
        /// Gets the target version range for <see cref="TargetAssembly"/>.
        /// </summary>
        public IntegrationVersionRange TargetVersionRange { get; } = new IntegrationVersionRange();

        /// <summary>
        /// Gets or sets the target minimum version.
        /// </summary>
        public string TargetMinimumVersion
        {
            get => TargetVersionRange.MinimumVersion;
            set => TargetVersionRange.MinimumVersion = value;
        }

        /// <summary>
        /// Gets or sets the target maximum version.
        /// </summary>
        public string TargetMaximumVersion
        {
            get => TargetVersionRange.MaximumVersion;
            set => TargetVersionRange.MaximumVersion = value;
        }
    }
}
