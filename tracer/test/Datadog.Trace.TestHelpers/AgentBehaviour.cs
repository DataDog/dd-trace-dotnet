// <copyright file="AgentBehaviour.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers
{
    public enum AgentBehaviour
    {
        /// <summary>
        /// Normal agent behaviour
        /// </summary>
        Normal = 0,

        /// <summary>
        /// The agent doesn't answer
        /// </summary>
        NoAnswer = 1,

        /// <summary>
        /// The agent answers after a long time
        /// </summary>
        SlowAnswer = 2,

        /// <summary>
        /// The agent answers with wrong data
        /// </summary>
        WrongAnswer = 3,

        /// <summary>
        /// The agent answers with 404 code
        /// </summary>
        Return404 = 4,

        /// <summary>
        /// The agent answers with 500 code
        /// </summary>
        Return500 = 5,
    }
}
