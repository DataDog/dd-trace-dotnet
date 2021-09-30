// <copyright file="Visitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.DataFormat
{
    internal static class Visitor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Visitor));

        public static bool DepthFirstSearch(object node, Predicate<string> func)
        {
            switch (node)
            {
                case Dictionary<string, object> dict:
                    foreach (var value in dict.Values)
                    {
                        if (DepthFirstSearch(value, func))
                        {
                            return true;
                        }
                    }

                    break;
                case Dictionary<string, string> dict:
                    foreach (var s in dict.Values)
                    {
                        if (func(s))
                        {
                            return true;
                        }
                    }

                    break;
                case List<object> list:
                    foreach (var value in list)
                    {
                        if (DepthFirstSearch(value, func))
                        {
                            return true;
                        }
                    }

                    break;
                case List<string> list:
                    foreach (var s in list)
                    {
                        if (func(s))
                        {
                            return true;
                        }
                    }

                    break;
                case string s:
                    if (func(s))
                    {
                        return true;
                    }

                    break;

                default:
                    Log.Warning("Ignoring unknown value of type: {Type}", node?.GetType());
                    break;
            }

            return false;
        }
    }
}
