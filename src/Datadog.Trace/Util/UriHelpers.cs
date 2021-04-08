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

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() instead. " +
                  "Kept for backwards compatibility where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetRelativeUrl(Uri uri, bool tryRemoveIds)
            => GetRelativeUrl(uri.AbsolutePath, tryRemoveIds);

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() instead. " +
                  "Kept for backwards compatibility where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetRelativeUrl(string uri, bool tryRemoveIds)
            => tryRemoveIds ? GetCleanUriPath(uri) : uri;

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() instead. " +
                  "Kept for backwards compatibility where there is a version mismatch between manual and automatic instrumentation")]
        public static string CleanUriSegment(string absolutePath)
            => GetCleanUriPath(absolutePath);

        public static string GetCleanUriPath(Uri uri)
        {
            return GetCleanUriPath(uri.AbsolutePath);
        }

        public static string GetCleanUriPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || (absolutePath.Length == 1 && absolutePath[0] == '/'))
            {
                return absolutePath;
            }

            // Sanitized url will be at worse as long as the original
            var sb = StringBuilderCache.Acquire(absolutePath.Length);

            int previousIndex = 0;
            int index;
            int segmentLength;

            do
            {
                index = absolutePath.IndexOf('/', previousIndex);

                if (index == -1)
                {
                    // Last segment
                    segmentLength = absolutePath.Length - previousIndex;
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

                if (index != -1)
                {
                    sb.Append('/');
                }

                previousIndex = index + 1;
            }
            while (index != -1);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static bool IsIdentifierSegment(string absolutePath, int startIndex, int segmentLength)
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
                    case '-':
                        continue;
                    default:
                        return false;
                }
            }

            return containsNumber;
        }
    }
}
