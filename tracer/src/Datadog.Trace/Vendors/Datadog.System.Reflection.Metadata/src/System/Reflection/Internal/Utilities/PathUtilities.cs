// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PathUtilities
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    internal static class PathUtilities
  {
    private const char DirectorySeparatorChar = '\\';
    private const char AltDirectorySeparatorChar = '/';
    private const char VolumeSeparatorChar = ':';

    #nullable disable
    private static string s_platformSpecificDirectorySeparator;


    #nullable enable
    private static string PlatformSpecificDirectorySeparator
    {
      get
      {
        string directorySeparator = PathUtilities.s_platformSpecificDirectorySeparator;
        if (directorySeparator != null)
          return directorySeparator;
        char ch = Array.IndexOf<char>(Path.GetInvalidFileNameChars(), '*') >= 0 ? '\\' : '/';
        return PathUtilities.s_platformSpecificDirectorySeparator = ch.ToString();
      }
    }

    /// <summary>
    /// Returns the position in given path where the file name starts.
    /// </summary>
    /// <returns>-1 if path is null.</returns>
    internal static int IndexOfFileName(string path)
    {
      if (path == null)
        return -1;
      for (int index = path.Length - 1; index >= 0; --index)
      {
        switch (path[index])
        {
          case '/':
          case ':':
          case '\\':
            return index + 1;
          default:
            continue;
        }
      }
      return 0;
    }

    /// <summary>Get file name from path.</summary>
    /// <remarks>Unlike <see cref="M:System.IO.Path.GetFileName(System.String)" /> this method doesn't check for invalid path characters.</remarks>
    internal static string GetFileName(string path, bool includeExtension = true)
    {
      int startIndex = PathUtilities.IndexOfFileName(path);
      return startIndex > 0 ? path.Substring(startIndex) : path;
    }

    internal static string CombinePathWithRelativePath(string root, string relativePath)
    {
      if (root.Length == 0)
        return relativePath;
      switch (root[root.Length - 1])
      {
        case '/':
        case ':':
        case '\\':
          return root + relativePath;
        default:
          return root + PathUtilities.PlatformSpecificDirectorySeparator + relativePath;
      }
    }
  }
}
