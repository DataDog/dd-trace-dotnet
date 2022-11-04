// <copyright file="TaintedObjects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast
{
    internal class TaintedObjects
    {
        private ITaintedMap map;

        public TaintedObjects()
        {
            map = new DefaultTaintedMap();
        }

        public TaintedObjects(ITaintedMap map)
        {
            this.map = map;
        }

        public void TaintInputString(string obj, Source? source)
        {
            map.Put(new TaintedObject(obj, Ranges.ForString(obj, source)));
        }

        public void Taint(object obj, Range[]? ranges)
        {
            map.Put(new TaintedObject(obj, ranges));
        }

        public TaintedObject Get(object obj)
        {
            return map.Get(obj);
        }
    }
}
