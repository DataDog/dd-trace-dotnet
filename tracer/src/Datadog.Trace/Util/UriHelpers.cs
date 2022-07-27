// <copyright file="UriHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal static class UriHelpers
    {
        /// <summary>
        /// Remove the querystring, user information, and fragment from a URL.
        /// Optionally reduce cardinality by replacing segments that look like IDs with <c>?</c>.
        /// </summary>
        /// <param name="uri">The URI to clean</param>
        /// <param name="removeScheme">Should the scheme be removed?</param>
        /// <param name="tryRemoveIds">Should IDs be replaced with <c>?</c></param>
        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds)
        {
            var path = tryRemoveIds ? GetCleanUriPath(uri.AbsolutePath) : uri.AbsolutePath;

            if (removeScheme)
            {
                // keep only host and path.
                // remove scheme, userinfo, query, and fragment.
                return $"{uri.Authority}{path}";
            }

            // keep only scheme, authority, and path.
            // remove userinfo, query, and fragment.
            return $"{uri.Scheme}{Uri.SchemeDelimiter}{uri.Authority}{path}";
        }

        public static string GetCleanUriPath(Uri uri)
            => GetCleanUriPath(uri.AbsolutePath);

        public static string GetCleanUriPath(Uri uri, string virtualPathToRemove)
            => GetCleanUriPath(uri.AbsolutePath, virtualPathToRemove);

        public static string GetCleanUriPath(string absolutePath)
            => GetCleanUriPath(absolutePath, null);

        /// <summary>
        /// Cleans identifiers from an absolute path, and optionally removes the provided prefix
        /// </summary>
        /// <param name="absolutePath">The path to clean</param>
        /// <param name="virtualPathToRemove">The optional virtual path to remove from the front of the path</param>
        /// <returns>The cleaned path</returns>
        public static string GetCleanUriPath(string absolutePath, string virtualPathToRemove)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || (absolutePath.Length == 1 && absolutePath[0] == '/'))
            {
                return absolutePath;
            }

            if (!string.IsNullOrEmpty(virtualPathToRemove) && string.Equals(absolutePath, virtualPathToRemove))
            {
                return "/";
            }

            // If the virtual path is "/" then we're hosted at the root, so nothing to remove
            // If not, it will be of the form "/myapp", so remove whole thing
            // Make sure we only remove _whole_ segment i.e. /myapp/controller, but not /myappcontroller
            var hasPrefix = !string.IsNullOrEmpty(virtualPathToRemove)
                         && virtualPathToRemove != "/"
                         && virtualPathToRemove[0] == '/'
                         && absolutePath.StartsWith(virtualPathToRemove, StringComparison.OrdinalIgnoreCase)
                         && absolutePath.Length > virtualPathToRemove.Length
                         && absolutePath[virtualPathToRemove.Length] == '/';

            // Sanitized url will be at worse as long as the original, minus a removed virtual path
            var maxLength = absolutePath.Length - (hasPrefix ? virtualPathToRemove.Length : 0);
            var sb = StringBuilderCache.Acquire(maxLength);

            int previousIndex = hasPrefix ? virtualPathToRemove.Length : 0;
            int index;
            int segmentLength;
            int indexOfFileExtension = 0;

            do
            {
                index = absolutePath.IndexOf('/', previousIndex);

                if (index == -1)
                {
                    // Last segment
                    // Is this a filename with an extension?
                    if (absolutePath.Length > previousIndex
                     && (indexOfFileExtension = absolutePath.LastIndexOf('.')) != -1
                     && indexOfFileExtension > previousIndex)
                    {
                        // Only try and clean the filename, excluding the file extension
                        segmentLength = indexOfFileExtension - previousIndex;
                    }
                    else
                    {
                        segmentLength = absolutePath.Length - previousIndex;
                    }
                }
                else
                {
                    segmentLength = index - previousIndex;
                }

                if (IsIdentifierSegment(absolutePath, previousIndex, segmentLength))
                {
                    sb.Append('?');
                }
                else
                {
                    sb.Append(absolutePath, previousIndex, segmentLength);
                }

                if (index == -1)
                {
                    if (segmentLength > 0 && indexOfFileExtension > previousIndex)
                    {
                        // add the file extension
                        sb.Append(absolutePath, indexOfFileExtension, absolutePath.Length - indexOfFileExtension);
                    }
                }
                else
                {
                    sb.Append('/');
                }

                previousIndex = index + 1;
            }
            while (index != -1);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public static bool IsIdentifierSegment(string absolutePath, int startIndex, int segmentLength)
        {
            if (segmentLength == 0)
            {
                return false;
            }

            int lastIndex = startIndex + segmentLength;
            var containsNumber = false;

            for (int index = startIndex; index < lastIndex && index < absolutePath.Length; index++)
            {
                char c = absolutePath[index];

                switch (c)
                {
                    case >= '0' and <= '9':
                        containsNumber = true;
                        continue;
                    case >= 'a' and <= 'f':
                    case >= 'A' and <= 'F':
                        if (segmentLength < 16)
                        {
                            // don't be too aggressive replacing
                            // short hex segments like "/a" or "/cab",
                            // they are likely not ids
                            return false;
                        }

                        continue;
                    case ',':
                    case '-':
                        continue;
                    default:
                        return false;
                }
            }

            return containsNumber;
        }

        /// <summary>
        /// Combines an absolute base <see cref="Uri"/> <paramref name="baseUri"/> with a path <paramref name="relativePath"/>.
        /// If <paramref name="baseUri"/> includes a path component, this will be included in the final <see cref="Uri"/>.
        /// The final <see cref="Uri"/> will always contain all path segments from both parameters.
        /// </summary>
        /// <example>The following calls all return the <see cref="Uri"/> <c>http://host/a/b/c</c>.
        /// <code>Combine(new Uri("http://host/a/b"), "c");
        /// Combine(new Uri("http://host/a/b"), "/c");
        /// Combine(new Uri("http://host/a/b/"), "c");
        /// Combine(new Uri("http://host/a/b/"), "/c");</code></example>
        /// <param name="baseUri">The base <see cref="Uri"/>, which may or may not end with a <c>/</c>. </param>
        /// <param name="relativePath">The relative path, which may or may not start with a <c>/</c>.</param>
        /// <returns>The combined <see cref="Uri"/></returns>
        public static Uri Combine(Uri baseUri, string relativePath)
        {
            var builder = new UriBuilder(baseUri);
            builder.Path = Combine(builder.Path, relativePath);
            return builder.Uri;
        }

        /// <summary>
        /// Combines an absolute base <see cref="string"/> <paramref name="baseUri"/> with a path <paramref name="relativePath"/>.
        /// If <paramref name="baseUri"/> includes a path component, this will be included in the final <see cref="string"/>.
        /// The final <see cref="string"/> will always contain all path segments from both parameters.
        /// </summary>
        /// <example>The following calls all return the <see cref="Uri"/> <c>http://host/a/b/c</c>.
        /// <code>Combine(new Uri("http://host/a/b"), "c");
        /// Combine(new Uri("http://host/a/b"), "/c");
        /// Combine(new Uri("http://host/a/b/"), "c");
        /// Combine(new Uri("http://host/a/b/"), "/c");</code></example>
        /// <param name="baseUri">The base <see cref="string"/>, which may or may not end with a <c>/</c>. </param>
        /// <param name="relativePath">The relative path, which may or may not start with a <c>/</c>.</param>
        /// <returns>The combined <see cref="Uri"/></returns>
        public static string Combine(string baseUri, string relativePath)
            => baseUri.EndsWith("/")
                ? (relativePath.StartsWith("/")
                    ? $"{baseUri.Substring(0, baseUri.Length - 1)}{relativePath}"
                    : $"{baseUri}{relativePath}")
                : (relativePath.StartsWith("/")
                    ? $"{baseUri}{relativePath}"
                    : $"{baseUri}/{relativePath}");
    }
}
