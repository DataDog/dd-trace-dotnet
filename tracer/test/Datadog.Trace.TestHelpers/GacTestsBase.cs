// <copyright file="GacTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using NUnit.Framework;

namespace Datadog.Trace.TestHelpers
{
    [NonParallelizable]
    public abstract class GacTestsBase : TestHelper
    {
        protected GacTestsBase(string sampleAppName, string samplePathOverrides)
            : base(sampleAppName, samplePathOverrides)
        {
        }

        protected GacTestsBase(string sampleAppName)
            : base(sampleAppName)
        {
        }

        public void AddAssembliesToGac()
        {
#if NETFRAMEWORK
            var publish = new System.EnterpriseServices.Internal.Publish();

            var targetFolder = CustomTestFramework.GetProfilerTargetFolder();

            foreach (var file in Directory.GetFiles(targetFolder, "*.dll"))
            {
                publish.GacInstall(file);
            }
#endif
        }

        public void RemoveAssembliesFromGac()
        {
#if NETFRAMEWORK
            var publish = new System.EnterpriseServices.Internal.Publish();

            var targetFolder = CustomTestFramework.GetProfilerTargetFolder();

            foreach (var file in Directory.GetFiles(targetFolder, "*.dll"))
            {
                publish.GacRemove(file);
            }
#endif
        }
    }
}
