using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    internal static class UriHelpers
    {
        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds)
        {
            return CleanUri(uri, removeScheme, tryRemoveIds, useLegacyIdCleaning: true);
        }

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
                  "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetRelativeUrl(Uri uri, bool tryRemoveIds)
            => GetRelativeUrl(uri.AbsolutePath, tryRemoveIds);

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() instead. " +
                  "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static string GetRelativeUrl(string uri, bool tryRemoveIds)
            => tryRemoveIds ? GetCleanUriPath(uri) : uri;

        [Obsolete("This method is deprecated and will be removed. Use GetCleanUriPath() instead. " +
                  "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static string CleanUriSegment(string absolutePath)
            => GetCleanUriPath(absolutePath);

        public static string GetCleanUriPath(Uri uri) =>
            GetCleanUriPath(uri.AbsolutePath, useLegacyIdCleaning: true);

        public static string GetCleanUriPath(Uri uri, bool useLegacyIdCleaning) =>
            GetCleanUriPath(uri.AbsolutePath, useLegacyIdCleaning);

        public static string GetCleanUriPath(string absolutePath) =>
            GetCleanUriPath(absolutePath, useLegacyIdCleaning: true);

        public static string GetCleanUriPath(string absolutePath, bool useLegacyIdCleaning)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || (absolutePath.Length == 1 && absolutePath[0] == '/'))
            {
                return absolutePath;
            }

#if NETCOREAPP
            ReadOnlySpan<char> absPath = absolutePath.AsSpan();

            // Sanitized url will be at worse as long as the original
            var sb = StringBuilderCache.Acquire(absPath.Length);

            int previousIndex = 0;
            int index;

            do
            {
                ReadOnlySpan<char> nStart = absPath.Slice(previousIndex);
                index = nStart.IndexOf('/');
                ReadOnlySpan<char> segment = index == -1 ? nStart : nStart.Slice(0, index);

                // replace path segments that look like numbers or guid
                var isIdSegment = useLegacyIdCleaning
                     ? (long.TryParse(segment, out _) ||
                      (segment.Length == 32 && IsAGuid(segment, "N")) ||
                      (segment.Length == 36 && IsAGuid(segment, "D")))
                     : IsIdentifierSegment(segment);

                if (isIdSegment)
                {
                    sb.Append('?');
                }
                else
                {
                    sb.Append(segment);
                }

                if (index != -1)
                {
                    sb.Append('/');
                }

                previousIndex += index + 1;
            }
            while (index != -1);

            return StringBuilderCache.GetStringAndRelease(sb);
#else
            // Sanitized url will be at worse as long as the original
            var sb = StringBuilderCache.Acquire(absolutePath.Length);

            int previousIndex = 0;
            int index = 0;

            do
            {
                index = absolutePath.IndexOf('/', previousIndex);

                if (useLegacyIdCleaning)
                {
                    string segment;

                    if (index == -1)
                    {
                        // Last segment
                        segment = absolutePath.Substring(previousIndex);
                    }
                    else
                    {
                        segment = absolutePath.Substring(previousIndex, index - previousIndex);
                    }

                    // replace path segments that look like numbers or guid
                    // GUID format N "d85b1407351d4694939203acc5870eb1" length: 32
                    // GUID format D "d85b1407-351d-4694-9392-03acc5870eb1" length: 36 with dashes in indices 8, 13, 18 and 23.
                    if (long.TryParse(segment, out _) ||
                        (segment.Length == 32 && IsAGuid(segment, "N")) ||
                        (segment.Length == 36 && IsAGuid(segment, "D")))
                    {
                        segment = "?";
                    }

                    sb.Append(segment);
                }
                else
                {
                    int segmentLength;

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
                }

                if (index != -1)
                {
                    sb.Append("/");
                }

                previousIndex = index + 1;
            }
            while (index != -1);

            return StringBuilderCache.GetStringAndRelease(sb);
#endif
        }

#if NETCOREAPP
        internal static bool IsIdentifierSegment(ReadOnlySpan<char> segment)
        {
            if (segment.Length == 0)
            {
                return false;
            }

            var containsNumber = false;
            foreach (var c in segment)
            {
                switch (c)
                {
                    case >= '0' and <= '9':
                        containsNumber = true;
                        continue;
                    case >= 'a' and <= 'f':
                    case >= 'A' and <= 'F':
                        if (segment.Length < 16)
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
#else
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
#endif

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAGuid(string segment, string format) => Guid.TryParseExact(segment, format, out _);

#if NETCOREAPP
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAGuid(ReadOnlySpan<char> segment, string format) => Guid.TryParseExact(segment, format, out _);
#endif
    }
}
