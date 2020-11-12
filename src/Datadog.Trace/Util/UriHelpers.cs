using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Trace.Util
{
    internal static class UriHelpers
    {
        public const string UrlIdPlaceholder = "?";

        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds)
        {
            var path = GetRelativeUrl(uri, tryRemoveIds);

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

        public static string GetRelativeUrl(Uri uri, bool tryRemoveIds)
        {
            return GetRelativeUrl(uri.AbsolutePath, tryRemoveIds);
        }

        public static string GetRelativeUrl(string uri, bool tryRemoveIds)
        {
            // try to remove segments that look like ids
            return tryRemoveIds ? CleanUriSegment(uri) : uri;
        }

        public static string CleanUriSegment(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || (absolutePath.Length == 1 && absolutePath[0] == '/'))
            {
                return absolutePath;
            }

            // Sanitized url will be at worse as long as the original
            var sb = new StringBuilder(absolutePath.Length);

            int previousIndex = 0;
            int index = 0;

            while (index != -1)
            {
                index = absolutePath.IndexOf('/', previousIndex);

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
                    segment = UrlIdPlaceholder;
                }

                sb.Append(segment);

                if (index != -1)
                {
                    sb.Append("/");
                }

                previousIndex = index + 1;
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAGuid(string segment, string format) => Guid.TryParseExact(segment, format, out _);
    }
}
