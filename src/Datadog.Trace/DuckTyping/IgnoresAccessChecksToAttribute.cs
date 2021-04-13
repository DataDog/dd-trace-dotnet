namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// This attribute is recognized by the CLR and allow us to disable visibility checks for certain assemblies (only from 4.6+)
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IgnoresAccessChecksToAttribute"/> class.
        /// </summary>
        /// <param name="assemblyName">Assembly name</param>
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        /// <summary>
        /// Gets the assembly name
        /// </summary>
        public string AssemblyName { get; }
    }
}
