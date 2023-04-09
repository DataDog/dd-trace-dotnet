// <copyright file="TaintedObjects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast
{
    internal class TaintedObjects
    {
        private readonly ITaintedMap _map;

        public TaintedObjects()
        {
            _map = new DefaultTaintedMap();
        }

        public void TaintInputString(string stringToTaint, Source source)
        {
            if (!string.IsNullOrEmpty(stringToTaint))
            {
                _map.Put(new TaintedObject(stringToTaint, IastUtils.GetRangesForString(stringToTaint, source)));
            }
        }

        public void Taint(object objectToTaint, Range[] ranges)
        {
            if (objectToTaint is not null)
            {
                var objectAsString = objectToTaint as string;
                if (objectAsString is null || objectAsString != string.Empty)
                {
                    _map.Put(new TaintedObject(objectToTaint, ranges));
                }
            }
        }

        public TaintedObject? Get(object objectToFind)
        {
            return _map.Get(objectToFind) as TaintedObject;
        }
    }
}
