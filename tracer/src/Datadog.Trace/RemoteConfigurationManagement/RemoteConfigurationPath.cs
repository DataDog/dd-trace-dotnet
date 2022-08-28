// <copyright file="RemoteConfigurationPath.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;
using Datadog.Trace.Util;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal record RemoteConfigurationPath
    {
        private static readonly Regex PathRegex = new Regex("^(datadog/\\d+|employee)/([^/]+)/([^/]+)/[^/]+$", RegexOptions.Compiled);

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
            var matcher = PathRegex.Match(path);
            if (!matcher.Success)
            {
                ThrowHelper.ThrowException($"Error parsing path: {path}");
            }

            var product = matcher.Groups[2].Value;
            var id = matcher.Groups[3].Value;

            return new RemoteConfigurationPath(path, product, id);
        }
    }
}
