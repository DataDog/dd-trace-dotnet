// <copyright file="TeamcityEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

internal sealed class TeamcityEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: TeamCity detected");

        IsCI = true;
        Provider = "teamcity";
        JobName = ValueProvider.GetValue(Constants.TeamCityBuildConfName);
        JobUrl = ValueProvider.GetValue(Constants.TeamCityBuildUrl);
    }
}
