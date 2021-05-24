// <copyright file="ObscenelyAnnoyingGenericClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ObscenelyAnnoyingGenericClass<Tc1> : ObscenelyAnnoyingClass
    {
        public void Method(Tc1 i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method<Tm1>(Tc1 i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i, default(Tm1));
        }

        public void Method<Tm1>(Tm1 i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method<Tm1>(Tm1 i, Tc1 i2)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i, i2);
        }

        public void Method<Tm1>(Tc1 i, Tm1 i2)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i, i2);
        }
    }
}
