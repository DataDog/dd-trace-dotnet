// <copyright file="NameValueHeadersCollectionTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Headers;
using Xunit;

namespace Datadog.Trace.Tests.Headers
{
    public class NameValueHeadersCollectionTest
    {
        [Fact]
        public void ExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() => new NameValueHeadersCollection(null));
        }

        [Fact]
        public void CollectionTest()
        {
            var collection = new NameValueHeadersCollection(new NameValueCollection());
            collection.Add("foo", "bar");

            var values = collection.GetValues("foo");
            Assert.Single(values);
            Assert.Equal("bar", values.First());

            collection.Set("foo", "biz");
            values = collection.GetValues("foo");
            Assert.Single(values);
            Assert.Equal("biz", values.First());

            collection.Remove("foo");
            values = collection.GetValues("foo");
            Assert.Empty(values);

            collection.Add("foo", "bar");
            collection.Add("foo", "biz");
            values = collection.GetValues("foo");
            Assert.Equal(2, values.Count());
            Assert.Equal("bar", values.First());
            Assert.Equal("biz", values.Last());

            collection.Add("pierre", "paul");
            values = collection.GetValues("foo");
            Assert.Equal(2, values.Count());

            collection.Remove("foo");
            values = collection.GetValues("foo");
            Assert.Empty(values);
        }
    }
}
