using System;
using System.Reflection;

namespace Samples.DatabaseHelper
{
    public class AssemblyHelpers
    {
        public static Type LoadFileAndRetrieveType(Type originalType)
        {
            Assembly loadFileAssembly = Assembly.LoadFile(originalType.Assembly.Location);
            return loadFileAssembly.GetType(originalType.FullName);
        }
    }
}
