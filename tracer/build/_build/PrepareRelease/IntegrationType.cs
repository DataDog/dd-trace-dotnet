// <copyright file="IntegrationGroups.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace PrepareRelease
{
    public enum IntegrationType
    {
        /// <summary>
        /// Default calltarget integration
        /// </summary>
        Default = 0,

        /// <summary>
        /// Derived calltarget integration
        /// </summary>
        Derived = 1
    }
}
