// <copyright file="BodyExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class BodyExtractorTests
    {
        [Fact]
        public void TestVarietyOfEmptyPropertyTypes()
        {
            var target = new TestVarietyPoco();

            var result = BodyExtractor.GetKeysAndValues(target);

            foreach (var prop in target.GetType().GetProperties())
            {
                Assert.Equal(prop.GetValue(target)?.ToString(), result[prop.Name]?.ToString());
            }
        }

        [Fact]
        public void TestVarietyOfPropertyTypes()
        {
            var target = new TestVarietyPoco()
            {
                BooleanValue = true,
                ByteValue = 1,
                SByteValue = -1,
                CharValue = 'a',
                DecimalValue = -1,
                DoubleValue = 0.5,
                SingleValue = -0.5f,
                Int32Value = -1,
                UInt32Value = 1,
                IntPtrValue = -1,
                UIntPtrValue = 1,
                Int64Value = -1,
                UInt64Value = 1,
                Int16Value = -1,
                UShortValue = 1,
                StringValue = "hello"
            };

            var result = BodyExtractor.GetKeysAndValues(target);

            foreach (var prop in target.GetType().GetProperties())
            {
                Assert.Equal(prop.GetValue(target)?.ToString(), result[prop.Name]?.ToString());
            }
        }

        [Fact]
        public void TestIndexersAreIgnored()
        {
            var target = new TestIndexerPoco()
            {
                StringValue = "hello",
            };

            var result = BodyExtractor.GetKeysAndValues(target);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestWriteOnlyProperits()
        {
            var target = new TestWriteOnlyPropertyPoco()
            {
                StringValue = "hello",
            };

            var result = BodyExtractor.GetKeysAndValues(target);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestNestedObjectsBelowLimit()
        {
            var target = new TestNestedPropertiesPoco();
            PopulateTarget(target, WafConstants.MaxObjectDepth);

            var result = BodyExtractor.GetKeysAndValues(target);

            for (int i = 0; i < WafConstants.MaxObjectDepth - 1; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as Dictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestNestedObjectsAboveLimit()
        {
            var target = new TestNestedPropertiesPoco();
            PopulateTarget(target, WafConstants.MaxObjectDepth + 1);

            var result = BodyExtractor.GetKeysAndValues(target);

            for (int i = 0; i < WafConstants.MaxObjectDepth - 1; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as Dictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        private static void PopulateTarget(TestNestedPropertiesPoco target, int count)
        {
            var current = target;
            for (int i = 0; i < count; i++)
            {
                var next = new TestNestedPropertiesPoco();
                current.TestNestedPropertiesPocoValue = next;
                current = next;
            }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class TestVarietyPoco
    {
        public bool BooleanValue { get; set; }

        public byte ByteValue { get; set; }

        public sbyte SByteValue { get; set; }

        public char CharValue { get; set; }

        public decimal DecimalValue { get; set; }

        public double DoubleValue { get; set; }

        public float SingleValue { get; set; }

        public int Int32Value { get; set; }

        public uint UInt32Value { get; set; }

        public nint IntPtrValue { get; set; }

        public nuint UIntPtrValue { get; set; }

        public long Int64Value { get; set; }

        public ulong UInt64Value { get; set; }

        public short Int16Value { get; set; }

        public ushort UShortValue { get; set; }

        public Guid GuidValue { get; set; }

        public string StringValue { get; set; }
    }

    public class TestIndexerPoco
    {
        private List<string> indexerItems = new List<string>();

        public string StringValue { get; set; }

        public string this[int i]
        {
            get { return indexerItems[i]; }
            set { indexerItems[i] = value; }
        }
    }

    public class TestWriteOnlyPropertyPoco
    {
        public string StringValue { get; set; }

        public string WriteOnlyProp
        {
            set { }
        }
    }

    public class TestNestedPropertiesPoco
    {
        public TestNestedPropertiesPoco TestNestedPropertiesPocoValue { get; set; }
    }
}
