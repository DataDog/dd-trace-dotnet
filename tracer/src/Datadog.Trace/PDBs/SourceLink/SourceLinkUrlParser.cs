// <copyright file="SourceLinkUrlParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Pdb.SourceLink
{
    internal abstract class SourceLinkUrlParser
    {
        protected static IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(SourceLinkUrlParser));

        protected static bool IsValidCommitSha([NotNullWhen(true)] string? commitSha) => commitSha is { Length: 40 } && commitSha.All(char.IsLetterOrDigit);

        /// <summary>
        /// Extract the git commit sha and repository url from a GitHub SourceLink mapping string.
        /// For example, for the following SourceLink mapping string:
        ///     https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*
        /// It will return:
        ///     - commit sha: dd35903c688a74b62d1c6a9e4f41371c65704db8
        ///     - repository URL: https://github.com/DataDog/dd-trace-dotnet
        /// </summary>
        internal abstract bool TryParseSourceLinkUrl(Uri uri, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl);
    }
}
