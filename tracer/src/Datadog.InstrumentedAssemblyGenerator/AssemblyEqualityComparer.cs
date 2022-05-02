using System.Collections.Generic;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class AssemblyEqualityComparer : IEqualityComparer<IAssembly>
    {
        public bool Equals(IAssembly x, IAssembly y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            else if (x == null || y == null)
            {
                return false;
            }

            return x.Name == y.Name &&
                   x.Version.Major == y.Version.Major &&
                   x.Version.Minor == y.Version.Minor &&
                   PublicKeyBase.TokenEquals(x.PublicKeyOrToken, y.PublicKeyOrToken);
        }

        public int GetHashCode(IAssembly assembly)
        {
            unchecked
            {
                int hash = 27;
                hash = 13 * hash + assembly.Name.GetHashCode();
                hash = 13 * hash + assembly.Version.Major.GetHashCode();
                hash = 13 * hash + assembly.Version.Minor.GetHashCode();
                hash = 13 * hash + assembly.PublicKeyOrToken.Token.GetHashCode();
                return hash;
            }
        }
    }
}
