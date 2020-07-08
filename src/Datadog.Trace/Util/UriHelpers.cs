using System;
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
            // try to remove segments that look like ids
            string path = tryRemoveIds
                              ? CleanUriSegment(uri.AbsolutePath)
                              : uri.AbsolutePath;
            return path;
        }

        public static string CleanUriSegment(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
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
                segment = long.TryParse(segment, out _) ||
                    Guid.TryParseExact(segment, "N", out _) ||
                    Guid.TryParseExact(segment, "D", out _)
                        ? UrlIdPlaceholder
                        : segment;

                sb.Append(segment);

                if (index != -1)
                {
                    sb.Append("/");
                }

                previousIndex = index + 1;
            }

            return sb.ToString();
        }
    }
}
