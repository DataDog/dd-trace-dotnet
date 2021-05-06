using System;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [DatadogExporter]
    [MemoryDiagnoser]
    public class HttpBypassBenchmark
    {
        private static readonly TracerSettings TracerSettings;

        static HttpBypassBenchmark()
        {
            TracerSettings = new TracerSettings();
        }

        [Benchmark]
        public void CheckNoMatchStringReplace()
        {
            SkipResourceReplace("https://dd-netcore31-JUNKYARD.azurewebsites.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckMatchStringReplace()
        {
            SkipResourceReplace("https://dd-netcore31-JUNKYARD.applicationinsights.azure.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }


        [Benchmark]
        public void CheckNoMatchStringIndexOf()
        {
            SkipResourceIndexOf("https://dd-netcore31-JUNKYARD.azurewebsites.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckMatchStringIndexOf()
        {
            SkipResourceIndexOf("https://dd-netcore31-JUNKYARD.applicationinsights.azure.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckNoMatchStringContains()
        {
            SkipResourceContains("https://dd-netcore31-JUNKYARD.azurewebsites.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckMatchStringContains()
        {
            SkipResourceContains("https://dd-netcore31-JUNKYARD.applicationinsights.azure.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckNoMatchCharacterByCharacter()
        {
            SkipResourceCharacterByCharacter("https://dd-netcore31-JUNKYARD.azurewebsites.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckMatchCharacterByCharacter()
        {
            SkipResourceCharacterByCharacter("https://dd-netcore31-JUNKYARD.applicationinsights.azure.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckNoMatchCharacterByCharacterWithShortcutting()
        {
            SkipResourceCharacterByCharacterWithShortcutting("https://dd-netcore31-JUNKYARD.azurewebsites.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        [Benchmark]
        public void CheckMatchCharacterByCharacterWithShortcutting()
        {
            SkipResourceCharacterByCharacterWithShortcutting("https://dd-netcore31-JUNKYARD.applicationinsights.azure.net/?somerandomquerystring=123456789012345678901234567890", TracerSettings.HttpClientExcludedUrlPatterns);
        }

        public static bool SkipResourceCharacterByCharacter(string requestUri, string[] patternsToSkip)
        {
            requestUri = requestUri.ToLowerInvariant();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                int matchCount = 0;

                for (var characterIndex = 0; characterIndex < patternsToSkip[index].Length; characterIndex++)
                {
                    for (var uriIndex = 0; uriIndex < requestUri.Length; uriIndex++)
                    {
                        if (patternsToSkip[index][characterIndex].Equals(requestUri[uriIndex]))
                        {
                            matchCount++;
                        }

                        if (matchCount == patternsToSkip[index].Length)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool SkipResourceCharacterByCharacterWithShortcutting(string requestUri, string[] patternsToSkip)
        {
            requestUri = requestUri.ToLowerInvariant();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                int matchCount = 0;

                for (var characterIndex = 0; characterIndex < patternsToSkip[index].Length; characterIndex++)
                {
                    int remaining = requestUri.Length;

                    for (var uriIndex = 0; uriIndex < requestUri.Length; uriIndex++)
                    {
                        if (patternsToSkip[index][characterIndex].Equals(requestUri[uriIndex]))
                        {
                            matchCount++;
                        }

                        if (remaining - matchCount < patternsToSkip[index].Length)
                        {
                            // Not enough left to match up
                            break;
                        }

                        if (matchCount == patternsToSkip[index].Length)
                        {
                            return true;
                        }

                        remaining--;
                    }
                }
            }

            return false;
        }

        public static bool SkipResourceReplace(string requestUri, string[] patternsToSkip)
        {
            requestUri = requestUri.ToLower();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                if ((requestUri.Length - requestUri.Replace(patternsToSkip[index], string.Empty).Length) / patternsToSkip[index].Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SkipResourceContains(string requestUri, string[] patternsToSkip)
        {
            requestUri = requestUri.ToLower();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                if (requestUri.Contains(patternsToSkip[index]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SkipResourceIndexOf(string requestUri, string[] patternsToSkip)
        {
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                if (requestUri.IndexOf(patternsToSkip[index], StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
