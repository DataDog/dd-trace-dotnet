// <copyright file="Return.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class Return
    {
        internal bool Blocked { get; private set; }

        internal ResultData ResultData { get; set; }

        internal static Return From(IResult wafReturn)
        {
            return new Return
            {
                ResultData = JsonConvert.DeserializeObject<ResultData[]>(wafReturn.Data).FirstOrDefault(),
                Blocked = wafReturn.ReturnCode == ReturnCode.Block
            };
        }
    }
}
