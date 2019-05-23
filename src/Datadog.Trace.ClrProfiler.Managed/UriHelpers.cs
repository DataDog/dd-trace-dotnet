using System;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static class UriHelpers
    {
        public const string UrlIdPlaceholder = "?";

        public static string CleanUri(Uri uri, bool removeScheme, bool tryRemoveIds)
        {
            // try to remove segments that look like ids
            string path = tryRemoveIds
                              ? string.Concat(uri.Segments.Select(CleanUriSegment))
                              : uri.AbsolutePath;

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

        public static string CleanUriSegment(string segment)
        {
            bool hasTrailingSlash = segment.EndsWith("/", StringComparison.Ordinal);

            // remove trailing slash
            if (hasTrailingSlash)
            {
                segment = segment.Substring(0, segment.Length - 1);
            }

            // remove path segments that look like int or guid (with or without dashes)
            segment = int.TryParse(segment, out _) ||
                      Guid.TryParseExact(segment, "N", out _) ||
                      Guid.TryParseExact(segment, "D", out _)
                          ? UrlIdPlaceholder
                          : segment;

            return hasTrailingSlash
                       ? segment + "/"
                       : segment;
        }
    }
}
