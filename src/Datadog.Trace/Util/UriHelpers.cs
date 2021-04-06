using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    internal static class UriHelpers
    {
        [Obsolete("This method is deprecated and will be removed. Use CleanUri() that specifies useLegacyIdCleaning instead. " +
                  "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds)
        {
            return CleanUri(uri, removeScheme, tryRemoveIds, useLegacyIdCleaning: true);
        }

        /// <summary>
        /// Remove the querystring, user information, and fragment from a URL.
        /// Optionally reduce cardinality by replacing segments that look like IDs with <c>?</c>.
        /// </summary>
        /// <param name="uri">The URI to clean</param>
        /// <param name="removeScheme">Should the scheme be removed?</param>
        /// <param name="tryRemoveIds">Should IDs be replaced with <c>?</c></param>
        /// <param name="useLegacyIdCleaning">Should we use the legacy ID cleaning method.
        /// The legacy method only considers <c>long</c>s and <c>Guid</c>s to be valid IDs.
        /// The new method is more lenient. New usages of this method should set this value to <c>false</c>
        /// </param>
        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds, bool useLegacyIdCleaning)
        {
            var path = tryRemoveIds ? GetCleanUriPath(uri.AbsolutePath, useLegacyIdCleaning) : uri.AbsolutePath;

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

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() that specifies useLegacyIdCleaning instead. " +
                  "Kept for backwards compatibility where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetCleanUriPath(Uri uri) =>
            GetCleanUriPath(uri.AbsolutePath, useLegacyIdCleaning: true);

        public static string GetCleanUriPath(Uri uri, bool useLegacyIdCleaning) =>
            GetCleanUriPath(uri.AbsolutePath, useLegacyIdCleaning);

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() that specifies useLegacyIdCleaning instead. " +
                  "Kept for backwards compatibility where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetCleanUriPath(string absolutePath) =>
            GetCleanUriPath(absolutePath, useLegacyIdCleaning: true);

        public static string GetCleanUriPath(string absolutePath, bool useLegacyIdCleaning)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || (absolutePath.Length == 1 && absolutePath[0] == '/'))
            {
                return absolutePath;
            }

            // Sanitized url will be at worse as long as the original
            var sb = StringBuilderCache.Acquire(absolutePath.Length);

#if NETCOREAPP
            var absolutePathSpan = absolutePath.AsSpan();
#endif

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

                bool isIdSegment;

                if (useLegacyIdCleaning)
                {
#if NETCOREAPP
                    ReadOnlySpan<char> segment =
                        index == -1
                            ? absolutePathSpan.Slice(previousIndex)
                            : absolutePathSpan.Slice(previousIndex, segmentLength);

                    isIdSegment = long.TryParse(segment, out _) ||
                                  (segment.Length == 32 && IsAGuid(segment, "N")) ||
                                  (segment.Length == 36 && IsAGuid(segment, "D"));
#else
                    var segment = index == -1
                                      ? absolutePath.Substring(previousIndex)
                                      : absolutePath.Substring(previousIndex, index - previousIndex);
                    isIdSegment = long.TryParse(segment, out _) ||
                                     (segment.Length == 32 && IsAGuid(segment, "N")) ||
                                     (segment.Length == 36 && IsAGuid(segment, "D"));
#endif
                }
                else
                {
                    isIdSegment = IsIdentifierSegment(absolutePath, previousIndex, segmentLength);
                }

                if (isIdSegment)
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAGuid(string segment, string format) => Guid.TryParseExact(segment, format, out _);

#if NETCOREAPP
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAGuid(ReadOnlySpan<char> segment, string format) => Guid.TryParseExact(segment, format, out _);
#endif
    }
}
