using System;
using System.Collections.Generic;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class SpanDecorationMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public SpanDecorationMethodProbeTestDataAttribute
            (
            string returnTypeName = null,
            string[] parametersTypeName = null,
            bool skip = false,
            int phase = 1,
            bool unlisted = false,
            int expectedNumberOfSnapshots = 1,
            bool useFullTypeName = true,
            string whenJson = null,
            string[] decorationJson = null,
            string[] decorationTagName = null,
            string probeId = null,
            bool captureSnapshot = true,
            string evaluateAt = null,
            bool expectProbeStatusFailure = false,
            params string[] skipOnFrameworks)
            : base(returnTypeName: returnTypeName, parametersTypeName: parametersTypeName, skip: skip, phase: phase, unlisted: unlisted, expectedNumberOfSnapshots: expectedNumberOfSnapshots, useFullTypeName: useFullTypeName, conditionJson: null, templateJson: null, templateStr: null, probeId: probeId, evaluateAt: evaluateAt, captureSnapshot: captureSnapshot, expectProbeStatusFailure: expectProbeStatusFailure, skipOnFrameworks: skipOnFrameworks)
        {
            if (decorationJson.Length != decorationTagName.Length)
            {
                throw new ArgumentException("decoration json and decoration tag name should have the same length");
            }

            WhenJson = whenJson;
            Decorations = new KeyValuePair<string, string>[decorationJson.Length];
            for (int i = 0; i < decorationJson.Length; i++)
            {
                Decorations[i] = new KeyValuePair<string, string>(decorationTagName[i], decorationJson[i]);
            }
        }

        public string WhenJson { get; set; }
        public KeyValuePair<string, string>[] Decorations { get; set; }
    }
}
