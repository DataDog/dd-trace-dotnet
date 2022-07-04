using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal static class Utils
    {
        /// <summary>
        /// Replace all invalid chars in file name
        /// </summary>
        /// <param name="file"></param>
        /// <param name="replaceWith"></param>
        /// <returns></returns>
        internal static string CleanFileName(this FileInfo file, string replaceWith = "")
        {
            return Path.GetInvalidFileNameChars().
                Concat(Path.GetInvalidPathChars()).
                Aggregate(file.Name, (current, c) => current.Replace(c.ToString(), replaceWith));
        }

        /// <summary>
        /// Delete all content of directory and throw exception on failure
        /// </summary>
        internal static void ClearDirectory(this DirectoryInfo directoryInfo, bool includeSelf = false, bool includeDirectories = true, Func<string, bool> excludeFiles = null, Func<string, bool> excludeDirectories = null)
        {
            foreach (FileInfo file in directoryInfo.EnumerateFiles())
            {
                if (excludeFiles?.Invoke(file.FullName) == true)
                {
                    continue;
                }

                try
                {
                    file.Delete();
                }
                catch (Exception e)
                {
                    // https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#maximum-path-length-limitation
                    if (file.FullName.Length > 259)
                    {
                        e.Data.Add("reason", $"The path is too long, enable long path or move the file to a short path. {file.FullName} \r\nhttps://docs.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation");
                    }
                    throw;
                }
            }

            if (!includeDirectories)
            {
                return;
            }

            foreach (DirectoryInfo dir in directoryInfo.EnumerateDirectories())
            {
                if (excludeDirectories?.Invoke(dir.FullName) == true)
                {
                    continue;
                }

                try
                {
                    ClearDirectory(dir);
                    dir.Delete();
                }
                catch (Exception e)
                {
                    // https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#maximum-path-length-limitation
                    if (dir.FullName.Length > 259)
                    {
                        e.Data.Add("reason", "The path is too long, enable long path or move the directory to a short path. \r\nhttps://docs.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation");
                    }
                    throw;
                }
            }
        }

        public static bool Compare(this IAssembly first, IAssembly second)
        {
            return first.Name == second.Name &&
                   first.Version.Major == second.Version.Major &&
                   first.Version.Minor == second.Version.Minor &&
                   PublicKeyBase.TokenEquals(first.PublicKeyOrToken, second.PublicKeyOrToken);
        }

        public static IEnumerable<(string name, CorLibTypeSig sig)> GetAllTypes(this ICorLibTypes corLibTypes)
        {
            return from property in corLibTypes.GetType().GetProperties()
                   let value = property.GetGetMethod().Invoke(corLibTypes, null)
                   where value is CorLibTypeSig
                   select (property.Name, (CorLibTypeSig)value);
        }

        public static IEnumerable<T> Enumerate<T>(this T toEnumerate)
        {
            yield return toEnumerate;
        }

        /// <summary>
        /// Gets all usable exported types from the given assembly.
        /// </summary>
        /// <param name="assembly">Assembly to scan.</param>
        /// <returns>Usable types from the given assembly.</returns>
        /// <remarks>Types which cannot be loaded are skipped.</remarks>
        public static Type[] SafeGetTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var typeList = new List<Type>();
                foreach (Type type in ex.Types)
                {
                    if (type != null)
                    {
                        typeList.Add(type);
                    }
                }
                return typeList.ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
