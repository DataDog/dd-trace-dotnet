// <copyright file="PathUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class PathUtil
{
    /// <summary>
    /// Create a relative path from one path to another. Paths will be resolved before calculating the difference.
    /// Default path comparison for the active platform will be used (OrdinalIgnoreCase for Windows or Mac, Ordinal for Unix).
    /// </summary>
    /// <param name="relativeTo">The source path the output should be relative to. This path is always considered to be a directory.</param>
    /// <param name="path">The destination path.</param>
    /// <returns>The relative path or <paramref name="path"/> if the paths don't share the same root.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="relativeTo"/> or <paramref name="path"/> is <c>null</c> or an empty string.</exception>
    public static string GetRelativePath(string relativeTo, string path)
#if NETCOREAPP
        => Path.GetRelativePath(relativeTo, path);
#else
    {
        const char directorySeparatorChar = '\\';
        const char altDirectorySeparatorChar = '/';
        const char volumeSeparatorChar = ':';

        const string extendedDevicePathPrefix = @"\\?\";
        const string uncExtendedPathPrefix = @"\\?\UNC\";

        // based on https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.Private.CoreLib/src/System/IO/Path.cs#L843
        var comparisonType = StringComparison.OrdinalIgnoreCase; // Windows only
        relativeTo = Path.GetFullPath(relativeTo);
        path = Path.GetFullPath(path);

        // Need to check if the roots are different- if they are we need to return the "to" path.
        if (!AreRootsEqual(relativeTo, path, comparisonType))
        {
            return path;
        }

        var commonLength = GetCommonPathLength(relativeTo, path, ignoreCase: true);

        // If there is nothing in common they can't share the same root, return the "to" path as is.
        if (commonLength == 0)
        {
            return path;
        }

        // Trailing separators aren't significant for comparison
        var relativeToLength = relativeTo.Length;
        if (EndsInDirectorySeparator(relativeTo))
        {
            relativeToLength--;
        }

        var pathEndsInSeparator = EndsInDirectorySeparator(path);
        var pathLength = path.Length;
        if (pathEndsInSeparator)
        {
            pathLength--;
        }

        // If we have effectively the same path, return "."
        if (relativeToLength == pathLength && commonLength >= relativeToLength)
        {
            return ".";
        }

        // We have the same root, we need to calculate the difference now using the
        // common Length and Segment count past the length.
        //
        // Some examples:
        //
        //  C:\Foo C:\Bar L3, S1 -> ..\Bar
        //  C:\Foo C:\Foo\Bar L6, S0 -> Bar
        //  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
        //  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar

        var sb = new StringBuilder();
        sb.EnsureCapacity(Math.Max(relativeTo.Length, path.Length));

        // Add parent segments for segments past the common on the "from" path
        if (commonLength < relativeToLength)
        {
            sb.Append("..");

            for (var i = commonLength + 1; i < relativeToLength; i++)
            {
                if (IsDirectorySeparator(relativeTo[i]))
                {
                    sb.Append(directorySeparatorChar);
                    sb.Append("..");
                }
            }
        }
        else if (IsDirectorySeparator(path[commonLength]))
        {
            // No parent segments and we need to eat the initial separator
            //  (C:\Foo C:\Foo\Bar case)
            commonLength++;
        }

        // Now add the rest of the "to" path, adding back the trailing separator
        var differenceLength = pathLength - commonLength;
        if (pathEndsInSeparator)
        {
            differenceLength++;
        }

        if (differenceLength > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(directorySeparatorChar);
            }

            sb.Append(path, commonLength, differenceLength);
        }

        return sb.ToString();

        static int GetCommonPathLength(string first, string second, bool ignoreCase)
        {
            var commonChars = EqualStartingCharacterCount(first, second, ignoreCase: ignoreCase);

            // If nothing matches
            if (commonChars == 0)
            {
                return commonChars;
            }

            // Or we're a full string and equal length or match to a separator
            if (commonChars == first.Length
             && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
            {
                return commonChars;
            }

            if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
            {
                return commonChars;
            }

            // It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
            while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
            {
                commonChars--;
            }

            return commonChars;
        }

        static int EqualStartingCharacterCount(string first, string second, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            {
                return 0;
            }

            var commonChars = 0;
            var f = 0;
            var s = 0;

            var l = first[f];
            var r = second[s];
            var leftEnd = first.Length;
            var rightEnd = second.Length;

            while (l < leftEnd
                && r < rightEnd
                && (l == r || (ignoreCase && char.ToUpperInvariant(l) == char.ToUpperInvariant(r))))
            {
                commonChars++;
                l++;
                r++;
            }

            return commonChars;
        }

        static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
        {
            var firstRootLength = GetRootLength(first);
            var secondRootLength = GetRootLength(second);

            return firstRootLength == secondRootLength
                && string.Compare(
                       strA: first,
                       indexA: 0,
                       strB: second,
                       indexB: 0,
                       length: firstRootLength,
                       comparisonType: comparisonType) == 0;
        }

        static int GetRootLength(string path)
        {
            var i = 0;
            var volumeSeparatorLength = 2; // Length to the colon "C:"
            var uncRootLength = 2; // Length to the start of the server name "\\"

            var extendedSyntax = path.StartsWith(extendedDevicePathPrefix);
            var extendedUncSyntax = path.StartsWith(uncExtendedPathPrefix);
            if (extendedSyntax)
            {
                // Shift the position we look for the root from to account for the extended prefix
                if (extendedUncSyntax)
                {
                    // "\\" -> "\\?\UNC\"
                    uncRootLength = uncExtendedPathPrefix.Length;
                }
                else
                {
                    // "C:" -> "\\?\C:"
                    volumeSeparatorLength += extendedDevicePathPrefix.Length;
                }
            }

            if ((!extendedSyntax || extendedUncSyntax) && path.Length > 0 && IsDirectorySeparator(path[0]))
            {
                // UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")

                i = 1; // Drive rooted (\foo) is one character
                if (extendedUncSyntax || (path.Length > 1 && IsDirectorySeparator(path[1])))
                {
                    // UNC (\\?\UNC\ or \\), scan past the next two directory separators at most
                    // (e.g. to \\?\UNC\Server\Share or \\Server\Share\)
                    i = uncRootLength;
                    var n = 2; // Maximum separators to skip
                    while (i < path.Length && (!IsDirectorySeparator(path[i]) || --n > 0))
                    {
                        i++;
                    }
                }
            }
            else if (path.Length >= volumeSeparatorLength &&
                     path[volumeSeparatorLength - 1] == volumeSeparatorChar)
            {
                // Path is at least longer than where we expect a colon, and has a colon (\\?\A:, A:)
                // If the colon is followed by a directory separator, move past it
                i = volumeSeparatorLength;
                if (path.Length >= volumeSeparatorLength + 1 && IsDirectorySeparator(path[volumeSeparatorLength]))
                {
                    i++;
                }
            }

            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsDirectorySeparator(char c)
        {
            return c == directorySeparatorChar || c == altDirectorySeparatorChar;
        }

        static bool EndsInDirectorySeparator(string path)
            => path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);
    }
#endif
}
