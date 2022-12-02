// <copyright file="OtlpHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.Activity
{
    internal static class OtlpHelpers
    {
        internal static bool? GoStrConvParseBool(string value) =>
            value switch
            {
                "1" or "t" or "T" or "TRUE" or "true" or "True" => true,
                "0" or "f" or "F" or "FALSE" or "false" or "False" => false,
                _ => null
            };

        internal static void SetTagObject(Span span, string key, object? value)
        {
            if (value is null)
            {
                span.SetTag(key, null);
                return;
            }

            switch (value)
            {
                case char c: // TODO: Can't get here from OTEL API, test with Activity API
                    SetOtlpTag(span, key, c.ToString());
                    break;
                case string s:
                    SetOtlpTag(span, key, s);
                    break;
                case bool b:
                    SetOtlpTag(span, key, b ? "true" : "false");
                    break;
                case byte b: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, b);
                    break;
                case sbyte sb: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, sb);
                    break;
                case short sh: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, sh);
                    break;
                case ushort us: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, us);
                    break;
                case int i: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, i);
                    break;
                case uint ui: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, ui);
                    break;
                case long l: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, l);
                    break;
                case ulong ul: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, ul);
                    break;
                case float f: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, f);
                    break;
                case double d:
                    span.SetMetric(key, d);
                    break;
                case Array array:
                    SetOtlpTag(span, key, ComposeArrayString(array));
                    break;
                default:
                    SetOtlpTag(span, key, value.ToString()!);
                    break;
            }
        }

        // Use trace agent algorithm to transform Otlp attributes to Datadog span meta fields
        // See https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L424
        internal static void SetOtlpTag(Span span, string key, string value)
        {
            switch (key)
            {
                case "operation.name":
                    span.OperationName = value;
                    break;
                case "service.name":
                    span.ServiceName = value;
                    break;
                case "resource.name":
                    span.ResourceName = value;
                    break;
                case "span.type":
                    span.Type = value;
                    break;
                case "analytics.event":
                    if (GoStrConvParseBool(value) is bool b)
                    {
                        span.SetMetric(Tags.Analytics, b ? 1 : 0);
                    }

                    break;
                case "otel.status_code":
                    var newStatusCodeString = value switch
                    {
                        null => "STATUS_CODE_UNSET",
                        "ERROR" => "STATUS_CODE_ERROR",
                        "UNSET" => "STATUS_CODE_UNSET",
                        "OK" => "STATUS_CODE_OK",
                        string s => s,
                    };
                    span.SetTag(key, newStatusCodeString);
                    break;
                default:
                    span.SetTag(key, value);
                    break;
            }
        }

        internal static string ComposeArrayString(Array array)
        {
            if (array.Length == 0)
            {
                return "[]";
            }

            StringBuilder sb = new();
            sb.Append('[');
            for (int i = 0; i < array.Length; i++)
            {
                var value = array.GetValue(i);
                if (value is null)
                {
                    sb.Append("null");
                }
                else if (value is string s)
                {
                    sb.Append('"');
                    sb.Append(s);
                    sb.Append('"');
                }
                else if (value is bool b)
                {
                    sb.Append(b ? "true" : "false");
                }
                else
                {
                    sb.Append(value.ToString());
                }

                sb.Append(',');
            }

            sb.Remove(sb.Length - 1, 1);
            sb.Append(']');
            return sb.ToString();
        }
    }
}
