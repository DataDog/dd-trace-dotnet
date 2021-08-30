using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class PackagedAssemblyLookup
    {
        // The size of this is expected to on the order of 1 to a dozen. For lookups we will just scroll though this.
        private List<Entry> _assemblies = null;

        public int Count { get { return _assemblies?.Count ?? 0; } }

        public class Entry
        {
            public Entry(AssemblyName assemblyName, string assemblyFilePath)
            {
                Validate.NotNull(assemblyName, nameof(assemblyName));
                Validate.NotNullOrWhitespace(assemblyFilePath, nameof(assemblyFilePath));

                this.AssemblyName = assemblyName;
                this.AssemblyFilePath = assemblyFilePath;
                this.IsProcessedFromPackage = false;
            }

            public string AssemblyFilePath { get; }
            public AssemblyName AssemblyName { get; }
            public bool IsProcessedFromPackage { get; set; }
        }

        public void Add(Entry packagedAssembly)
        {
            if (packagedAssembly == null)
            {
                return;
            }

            if (_assemblies == null)
            {
                _assemblies = new List<Entry>();
            }

            _assemblies.Add(packagedAssembly);
        }

        public bool TryFind(string assemblyName, out Entry packagedAssemblyInfo)
        {
            packagedAssemblyInfo = null;

            if (_assemblies == null || !DynamicLoader.TryParseAssemblyName(assemblyName, out AssemblyName lookupAssemblyName))
            {
                return false;
            } 

            foreach(Entry asmInfo in _assemblies)
            {
                if (IsMatch(lookupAssemblyName, asmInfo.AssemblyName))
                {
                    packagedAssemblyInfo = asmInfo;
                    return true;
                }
            }

            return false;
        }

        private static bool IsMatch(AssemblyName lookupAssemblyName, AssemblyName otherAssemblyName)
        {
            if (Object.ReferenceEquals(lookupAssemblyName, otherAssemblyName))
            {
                return true;
            }

            // If name strings FULLY match, then there is a match:

            if (!String.IsNullOrWhiteSpace(lookupAssemblyName.FullName)
                    && lookupAssemblyName.FullName.Equals(otherAssemblyName.FullName, StringComparison.Ordinal))
            {
                return true;
            }

            // Otherwise, to match, require ALL of:
            //  - Short name IS present AND it matches the other assembly
            //  - Culture name is either not present OR it matches the other assembly
            //  - Version is either not present OR it matches the other assembly
            //  - Public key token is is either not present OR it matches the other assembly

            if ( (!String.IsNullOrWhiteSpace(lookupAssemblyName.Name) && lookupAssemblyName.Name.Equals(otherAssemblyName.Name, StringComparison.Ordinal))
                    && (lookupAssemblyName.CultureName == null || lookupAssemblyName.CultureName.Equals(otherAssemblyName.CultureName, StringComparison.Ordinal))
                    && (lookupAssemblyName.Version == null || lookupAssemblyName.Version.Equals(otherAssemblyName.Version)))
            {
                byte[] pkt = lookupAssemblyName.GetPublicKeyToken();
                return pkt == null || pkt.IsEqual(otherAssemblyName.GetPublicKeyToken());
            }

            return false;
        }
    }
}
