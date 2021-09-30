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

        public static bool DepthFirstSearch(Dictionary<string, object> rootNode, Predicate<string> func)
        {
            var stack = new NodeHolder[10];
            var i = 0;
            stack[i] = new NodeHolder() { Loop = null, Node = rootNode };
            while (i >= 0)
            {
                switch (stack[i].Node)
                {
                    case Dictionary<string, object> dict:
                        stack[i].Loop ??= dict.Values.GetEnumerator();

                        if (stack[i].Loop.MoveNext())
                        {
                            var nextNode = stack[i].Loop.Current;
                            i++;
                            stack[i] = new NodeHolder() { Loop = null, Node = nextNode };
                        }
                        else
                        {
                            i--;
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

                        i--;
                        break;
                    case List<object> list:
                        stack[i].Loop ??= list.GetEnumerator();

                        if (stack[i].Loop.MoveNext())
                        {
                            var nextNode = stack[i].Loop.Current;
                            i++;
                            stack[i] = new NodeHolder() { Loop = null, Node = nextNode };
                        }
                        else
                        {
                            i--;
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

                        i--;
                        break;
                    case string s:
                        if (func(s))
                        {
                            return true;
                        }

                        i--;
                        break;

                    default:
                        Log.Warning("Ignoring unknown value of type: {Type}", stack[i].Node?.GetType());
                        break;
                }
            }

            return false;
        }

        private struct NodeHolder
        {
            public object Node;
            public IEnumerator<object> Loop;
        }
    }
}
