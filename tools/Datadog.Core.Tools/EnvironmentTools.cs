using System;
using System.IO;
using System.Linq;

namespace Datadog.Core.Tools
{
    /// <summary>
    /// General use utility methods for all tests and tools.
    /// </summary>
    public class EnvironmentTools
    {
        private static string _solutionDirectory = null;

        /// <summary>
        /// Find the solution directory from anywhere in the hierarchy.
        /// </summary>
        /// <returns>The solution directory.</returns>
        public static string GetSolutionDirectory()
        {
            if (_solutionDirectory == null)
            {
                var startDirectory = Environment.CurrentDirectory;
                var currentDirectory = Directory.GetParent(startDirectory);
                const string searchItem = @"Datadog.Trace.sln";

                while (true)
                {
                    var slnFile = currentDirectory.GetFiles(searchItem).SingleOrDefault();

                    if (slnFile != null)
                    {
                        break;
                    }

                    currentDirectory = currentDirectory.Parent;

                    if (currentDirectory == null || !currentDirectory.Exists)
                    {
                        throw new Exception($"Unable to find solution directory from: {startDirectory}");
                    }
                }

                _solutionDirectory = currentDirectory.FullName;
            }

            return _solutionDirectory;
        }
    }
}
