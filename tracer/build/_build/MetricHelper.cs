// <copyright file="GenerateIntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog;

public static class MetricHelper
{
    public static Task SendReportableErrorMetrics(ILogger log, Dictionary<string, int> errors)
    {
        if (errors.Count == 0)
        {
            return Task.CompletedTask;
        }

        const string metricName = "dd_trace_dotnet.ci.smoke_tests.reportable_errors";

        return SendMetric(log, metricName: metricName, errors.Select(kvp => CreatePoint(kvp.Key, kvp.Value)));

        static string CreatePoint(string errorReason, int count)
        {
            var tags = $$"""
                             "ci.stage:{{SanitizeTagValue(Environment.GetEnvironmentVariable("DD_LOGGER_SYSTEM_STAGEDISPLAYNAME"))}}",
                             "ci.job:{{SanitizeTagValue(Environment.GetEnvironmentVariable("DD_LOGGER_SYSTEM_JOBDISPLAYNAME"))}}",
                             "git.branch:{{SanitizeTagValue(Environment.GetEnvironmentVariable("DD_LOGGER_BUILD_SOURCEBRANCH"))}}",
                             "error_reason:{{SanitizeTagValue(errorReason)}}"
                         """;

            return $$"""
                     {
                         "metric": "{{metricName}}",
                         "type": 1,
                         "points": [{
                             "timestamp": {{((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()}},
                             "value": {{count}}
                             }],
                         "tags": [
                             {{tags}}
                         ]
                     }
                     """;
        }
    }

    private static async Task SendMetric(ILogger log, string metricName, IEnumerable<string> metrics)
    {
        var envKey = Environment.GetEnvironmentVariable("DD_LOGGER_DD_API_KEY");
        if (string.IsNullOrEmpty(envKey))
        {
            // We're probably not in CI
            log.Debug("No CI API Key found, skipping {MetricName} metric submission", metricName);
            return;
        }

        var payload = $$"""{ "series": [{{string.Join(",", metrics)}}] }""";

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("DD-API-KEY", envKey);

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.datadoghq.com/api/v2/series", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = response.IsSuccessStatusCode
                             ? "Successfully submitted metric"
                             : "Failed to submit metric";
            log.Warning("{Result} {MetricName}. Response was: Code: {ResponseStatusCode}. Response: {ResponseContent}. Payload sent was: \"{Payload}\"", result, metricName, response.StatusCode, responseContent, payload);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error sending {MetricName} metric to backend with payload \"{Payload}\"", metricName, payload);
        }
    }

    private static string SanitizeTagValue(string tag)
    {
        // Copied from
        // SpanTagHelper.TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag);
        return TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag) ? normalizedTag : tag;

        static bool TryNormalizeTagName(
            string value,
            bool normalizeSpaces,
            [NotNullWhen(returnValue: true)] out string normalizedTagName)
        {
            normalizedTagName = null;

            if (!IsValidTagName(value, out var trimmedValue))
            {
                return false;
            }

            var sb = new StringBuilder(trimmedValue.Length);
            sb.Append(trimmedValue.ToLowerInvariant());

            for (var x = 0; x < sb.Length; x++)
            {
                switch (sb[x])
                {
                    case (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '/' or '-':
                        continue;
                    case ' ' when !normalizeSpaces:
                        continue;
                    default:
                        sb[x] = '_';
                        break;
                }
            }

            normalizedTagName = sb.ToString();
            return true;
        }

        static bool IsValidTagName(
            string value,
            [NotNullWhen(returnValue: true)] out string trimmedValue)
        {
            trimmedValue = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmedTemp = value.Trim();

            if (!char.IsLetter(trimmedTemp[0]) || trimmedTemp.Length > 200)
            {
                return false;
            }

            trimmedValue = trimmedTemp;
            return true;
        }
    }
}
