// <copyright file="IHeaderDictionaryForTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    internal class IHeaderDictionaryForTest : Microsoft.AspNetCore.Http.IHeaderDictionary
    {
        private Dictionary<string, StringValues> _innerDic = new Dictionary<string, StringValues>();

        public ICollection<string> Keys => throw new NotImplementedException();

        public ICollection<StringValues> Values => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public long? ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public StringValues this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Add(string key, StringValues value)
        {
            throw new NotImplementedException();
        }

        public void Add(KeyValuePair<string, StringValues> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<string, StringValues> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
        {
            return _innerDic.GetEnumerator();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, StringValues> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string key, out StringValues value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
#endif
