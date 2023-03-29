// <copyright file="ApplyDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.RemoteConfigurationManagement;

internal struct ApplyDetails
{
    public ApplyDetails(string filename)
    {
        Filename = filename;
        ApplyState = ApplyStates.UNACKNOWLEDGED;
        Error = null;
    }

    public string Filename { get; }

    public uint ApplyState { get; set; }

    public string? Error { get; set; }

    public static ApplyDetails FromOk(string fileName)
    {
        var applyDetails = new ApplyDetails(fileName);
        if (applyDetails.ApplyState == ApplyStates.UNACKNOWLEDGED)
        {
            applyDetails.ApplyState = ApplyStates.ACKNOWLEDGED;
        }

        return applyDetails;
    }

    public static ApplyDetails FromError(string fileName, string? error) => new(fileName) { ApplyState = ApplyStates.ERROR, Error = error };

    public override string ToString()
    {
        return $"{Filename}, {ApplyState}, {Error}";
    }
}
