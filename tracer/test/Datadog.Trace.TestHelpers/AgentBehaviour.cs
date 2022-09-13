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
        NORMAL = 0,

        /// <summary>
        /// The agent doesn't answer
        /// </summary>
        NO_ANSWER = 1,

        /// <summary>
        /// The agent answers after a long time
        /// </summary>
        SLOW_ANSWER = 2,

        /// <summary>
        /// The agent answers with wrong data
        /// </summary>
        WRONG_ANSWER = 3,

        /// <summary>
        /// The agent answers with 404 code
        /// </summary>
        RETURN_404 = 4,

        /// <summary>
        /// The agent answers with 500 code
        /// </summary>
        RETURN_500 = 5,
    }
}
