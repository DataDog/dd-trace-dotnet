// <copyright file="IastModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.IAST
{
    internal class IastModule
    {
        public IastModule()
        {
        }

        public void OnHashingAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                return;
            }

            var algorithmId = algorithm.ToUpper();

            /*
            // get StackTraceElement for the caller of MessageDigest
            StackTraceElement stackTraceElement =
                stackWalker.walk(
                    stack->
                        stack
                            .filter(s-> !s.getClassName().equals("java.security.MessageDigest"))
                            .findFirst()
                            .get());
            */
            Vulnerability vulnerability = new Vulnerability(VulnerabilityType.WEAK_HASH, new Location("path", 4), new Evidence(algorithm));
        }
    }
}
