using System.Collections.Generic;

namespace Datadog.InstrumentedAssemblyGenerator
{
    /// <summary>
    /// Represents the modified assembly on disk, which contains all the metadata 
    /// changes and IL bytecode instrumentation that we performed at runtime.
    /// </summary>
    public record InstrumentedAssembly
    {
        /// <param name="exportPath">Path to the instrumented assembly</param>
        /// <param name="originalPath">Path to the original assembly</param>
        /// <param name="modifiedMethods">Instrumented methods - List of <see cref="ModifiedMethod"/></param>
        public InstrumentedAssembly(string exportPath, string originalPath, List<ModifiedMethod> modifiedMethods)
        {
            InstrumentedAssemblyPath = exportPath;
            OriginalAssemblyPath = originalPath;
            ModifiedMethods = modifiedMethods;
        }

        /// <summary>
        /// Path to the instrumented assembly
        /// </summary>
        public string InstrumentedAssemblyPath { get; }
        
        /// <summary>
        /// Path to the original assembly
        /// </summary>
        public string OriginalAssemblyPath { get; }

        /// <summary>
        /// Instrumented methods - List of <see cref="ModifiedMethod"/>
        /// </summary>
        public List<ModifiedMethod> ModifiedMethods { get; }
    }
}
