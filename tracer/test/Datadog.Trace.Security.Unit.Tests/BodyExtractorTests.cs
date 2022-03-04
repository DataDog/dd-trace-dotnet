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

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            foreach (var prop in target.GetType().GetProperties())
            {
                Assert.Equal(prop.GetValue(target)?.ToString(), result[prop.Name]?.ToString());
            }
        }

        [Fact]
        public void TestVarietyOfPropertyTypes()
        {
            var testTime = new DateTime(2022, 3, 1, 16, 0, 0);
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
                StringValue = "hello",
                GuidValue = Guid.NewGuid(),
                DateTimeValue = testTime,
                DateTimeOffsetValue = new DateTimeOffset(testTime),
                TimeSpanValue = TimeSpan.FromSeconds(12),
            };

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

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

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestWriteOnlyProperits()
        {
            var target = new TestWriteOnlyPropertyPoco()
            {
                StringValue = "hello",
            };

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestStruct()
        {
            var target = new TestStructPoco()
            {
                StringValue = "hello",
            };

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestNestedObjectsBelowLimit()
        {
            var target = new TestNestedPropertiesPoco();
            PopulateNestedTarget(target, WafConstants.MaxObjectDepth);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

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
            PopulateNestedTarget(target, WafConstants.MaxObjectDepth + 1);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < WafConstants.MaxObjectDepth - 1; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as Dictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestNullList()
        {
            var target = new TestListPoco()
            {
                TestList = null
            };

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.Null(items);
        }

        [Fact]
        public void TestListBelowLimit()
        {
            var target = new TestListPoco();
            PopulateListTarget(target, WafConstants.MaxMapOrArrayLength);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.Equal(WafConstants.MaxMapOrArrayLength, items.Count);

            for (int i = 0; i < WafConstants.MaxMapOrArrayLength - 1; i++)
            {
                Assert.Equal($"Prop{i}", items[i]);
            }
        }

        [Fact]
        public void TestListAboveLimit()
        {
            var target = new TestListPoco();
            PopulateListTarget(target, WafConstants.MaxMapOrArrayLength + 1);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxMapOrArrayLength, items.Count);

            for (int i = 0; i < WafConstants.MaxMapOrArrayLength; i++)
            {
                Assert.Equal($"Prop{i}", items[i]);
            }
        }

        [Fact]
        public void TestNullDictionary()
        {
            var target = new TestDictionaryPoco()
            {
                TestDictionary = null
            };

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.Null(items);
        }

        [Fact]
        public void TestDictionaryBelowLimit()
        {
            var target = new TestDictionaryPoco();
            PopulateDictionaryTarget(target, WafConstants.MaxMapOrArrayLength);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxMapOrArrayLength, items.Count);

            for (int i = 0; i < WafConstants.MaxMapOrArrayLength - 1; i++)
            {
                Assert.Equal($"Value{i}", items[$"Prop{i}"]);
            }
        }

        [Fact]
        public void TestDictionaryAboveLimit()
        {
            var target = new TestDictionaryPoco();
            PopulateDictionaryTarget(target, WafConstants.MaxMapOrArrayLength + 1);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxMapOrArrayLength, items.Count);

            for (int i = 0; i < WafConstants.MaxMapOrArrayLength - 1; i++)
            {
                Assert.Equal($"Value{i}", items[$"Prop{i}"]);
            }
        }

        [Fact]
        public void TestCyclicObjects()
        {
            var target = new TestNestedPropertiesPoco();
            var linker = new TestNestedPropertiesPoco()
            {
                TestNestedPropertiesPocoValue = target
            };

            target.TestNestedPropertiesPocoValue = linker;

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as Dictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestCyclicList()
        {
            var target = new TestNestedListPoco();
            var linker = new TestNestedListPoco()
            {
                TestList = new List<TestNestedListPoco> { target }
            };

            target.TestList.Add(linker);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                var items = result[nameof(target.TestList)] as List<object>;
                Assert.NotEmpty(items);
                result = items[0] as Dictionary<string, object>;
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestCyclicDictionary()
        {
            const string linkKey = "next";
            var target = new TestNestedDictionaryPoco();
            var linker = new TestNestedDictionaryPoco()
            {
                TestDictionary = new Dictionary<string, TestNestedDictionaryPoco> { { linkKey, target } }
            };

            target.TestDictionary.Add(linkKey, linker);

            var result = BodyExtractor.GetKeysAndValues(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;
                Assert.NotEmpty(items);
                result = items[linkKey] as Dictionary<string, object>;
            }

            Assert.Empty(result);
        }

        private static void PopulateNestedTarget(TestNestedPropertiesPoco target, int count)
        {
            var current = target;
            for (int i = 0; i < count; i++)
            {
                var next = new TestNestedPropertiesPoco();
                current.TestNestedPropertiesPocoValue = next;
                current = next;
            }
        }

        private static void PopulateListTarget(TestListPoco target, int count)
        {
            for (int i = 0; i < count; i++)
            {
                target.TestList.Add($"Prop{i}");
            }
        }

        private static void PopulateDictionaryTarget(TestDictionaryPoco target, int count)
        {
            for (int i = 0; i < count; i++)
            {
                target.TestDictionary.Add($"Prop{i}", $"Value{i}");
            }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1201 // Elements should appear in the correct order

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

        public DateTime DateTimeValue { get; set; }

        public DateTimeOffset DateTimeOffsetValue { get; set; }

        public TimeSpan TimeSpanValue { get; set; }

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

    public struct TestStructPoco
    {
        public string StringValue { get; set; }
    }

    public class TestNestedPropertiesPoco
    {
        public TestNestedPropertiesPoco TestNestedPropertiesPocoValue { get; set; }
    }

    public class TestListPoco
    {
        public List<string> TestList { get; set; } = new List<string>();
    }

    public class TestDictionaryPoco
    {
        public Dictionary<string, string> TestDictionary { get; set; } = new Dictionary<string, string>();
    }

    public class TestNestedListPoco
    {
        public List<TestNestedListPoco> TestList { get; set; } = new List<TestNestedListPoco>();
    }

    public class TestNestedDictionaryPoco
    {
        public Dictionary<string, TestNestedDictionaryPoco> TestDictionary { get; set; } = new Dictionary<string, TestNestedDictionaryPoco>();
    }
}
