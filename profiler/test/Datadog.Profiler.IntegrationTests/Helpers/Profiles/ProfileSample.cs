// <copyright file="ProfileSample.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class ProfileSample
    {
        internal ProfileSample(ProfileValues values, ProfileLabels labels, StackTrace callStack)
        {
            Values = values;
            Labels = labels;
            CallStack = callStack;
        }

        public ProfileValues Values { get; }

        public ProfileLabels Labels { get; }

        public StackTrace CallStack { get; }
    }
}
