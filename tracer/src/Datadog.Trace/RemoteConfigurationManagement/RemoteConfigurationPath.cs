// <copyright file="RemoteConfigurationPath.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed record RemoteConfigurationPath
    {
        private const string DatadogPrefix = "datadog/";
        private const string EmployeePrefix = "employee/";

        private RemoteConfigurationPath(string path, string product, string id)
        {
            Path = path;
            Product = product;
            Id = id;
        }

        public string Path { get; }

        public string Product { get; }

        public string Id { get; }

        public static RemoteConfigurationPath FromPath(string path)
        {
            // Previous regex: ^(datadog/\d+|employee)/([^/]+)/([^/]+)/[^/]+$
            // Determine prefix and find the start of the product segment.
            // "datadog/{digits}/{product}/{id}/{filename}"
            // "employee/{product}/{id}/{filename}"
            int productStart;

            if (path is null)
            {
                ThrowHelper.ThrowException("Error parsing path: path is null");
                return default; // unreachable, satisfies compiler
            }
            else if (path.StartsWith(DatadogPrefix, StringComparison.Ordinal))
            {
                // Find the slash after the digits: "datadog/{digits}/"
                var digitStart = DatadogPrefix.Length;
                var secondSlash = path.IndexOf('/', digitStart);
                if (secondSlash < 0 || secondSlash == digitStart)
                {
                    ThrowHelper.ThrowException($"Error parsing path: {path}");
                }

                // Verify all characters between are digits
                for (var i = digitStart; i < secondSlash; i++)
                {
                    if (path[i] is < '0' or > '9')
                    {
                        ThrowHelper.ThrowException($"Error parsing path: {path}");
                    }
                }

                productStart = secondSlash + 1;
            }
            else if (path.StartsWith(EmployeePrefix, StringComparison.Ordinal))
            {
                productStart = EmployeePrefix.Length;
            }
            else
            {
                ThrowHelper.ThrowException($"Error parsing path: {path}");
                return default; // unreachable, satisfies compiler
            }

            // From productStart we need exactly: {product}/{id}/{filename}
            // i.e. two more slashes, no empty segments, no trailing content after filename.
            var thirdSlash = path.IndexOf('/', productStart);
            if (thirdSlash < 0 || thirdSlash == productStart)
            {
                ThrowHelper.ThrowException($"Error parsing path: {path}");
            }

            var idStart = thirdSlash + 1;
            var fourthSlash = path.IndexOf('/', idStart);
            if (fourthSlash < 0 || fourthSlash == idStart)
            {
                ThrowHelper.ThrowException($"Error parsing path: {path}");
            }

            // Filename must be non-empty and there must be no more slashes
            var filenameStart = fourthSlash + 1;
            if (filenameStart >= path.Length || path.IndexOf('/', filenameStart) >= 0)
            {
                ThrowHelper.ThrowException($"Error parsing path: {path}");
            }

            var product = path.Substring(productStart, thirdSlash - productStart);
            var id = path.Substring(idStart, fourthSlash - idStart);

            return new RemoteConfigurationPath(path, product, id);
        }
    }
}
