// <copyright file="VerifyScrubber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using VerifyTests;

namespace Datadog.Trace.Security.IntegrationTests;

internal class VerifyScrubber
{
    private static readonly Regex AppSecFingerPrintSession = new(@"_dd.appsec.fp.session: ssn.[\s\-a-z0-9]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthenticationCollectionMode = new(@"_dd.appsec.user.collection_mode: .*,", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void ScrubAuthenticatedTags(VerifySettings settings)
    {
        // these tags are added by HttpContext.SetUser. After a login event it's not always called by all framework versions
        // we dont want to test authenticated tags here anyway, as they're tested by TestAuthenticatedRequest
        settings.AddRegexScrubber(AuthenticationCollectionMode, string.Empty);
        settings.AddRegexScrubber(AppSecFingerPrintSession, "_dd.appsec.fp.session: <SessionFp>");
    }
}
