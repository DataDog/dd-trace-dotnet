// <copyright file="ObjectExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class ObjectExtractorTests
    {
        private readonly string[] _fieldsAsStrings =
        {
            nameof(TestVarietyPoco.SByteValue),
            nameof(TestVarietyPoco.IntPtrValue),
            nameof(TestVarietyPoco.UIntPtrValue),
            nameof(TestVarietyPoco.CharValue),
            nameof(TestVarietyPoco.GuidValue),
            nameof(TestVarietyPoco.EnumValue),
            nameof(TestVarietyPoco.DateTimeValue),
            nameof(TestVarietyPoco.DateTimeOffsetValue),
            nameof(TestVarietyPoco.TimeSpanValue),
#if NET6_0_OR_GREATER
            nameof(TestVarietyPoco.TimeOnlyValue),
            nameof(TestVarietyPoco.DateOnlyValue)
#endif
        };

        [Fact]
        public void TestVarietyOfEmptyPropertyTypes()
        {
            var target = new TestVarietyPoco();

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            foreach (var prop in target.GetType().GetProperties())
            {
                Assert.Equal(_fieldsAsStrings.Contains(prop.Name) ? prop.GetValue(target).ToString() : prop.GetValue(target), result[prop.Name]);
            }
        }

        [Fact]
        public void TestVarietyOfPropertyTypes()
        {
            var testTime = new DateTime(2022, 3, 1, 16, 0, 0);
            var target = new TestVarietyPoco
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
                EnumValue = TestVarietyPoco.EnumValues.Value2
            };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            foreach (var prop in target.GetType().GetProperties())
            {
                var value = prop.GetValue(target);
                var expectedValue = _fieldsAsStrings.Contains(prop.Name) ? value.ToString() : value;
                Assert.Equal(expectedValue, result[prop.Name]);
            }
        }

        [Fact]
        public void TestIndexersAreIgnored()
        {
            var target = new TestIndexerPoco() { StringValue = "hello", };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestWriteOnlyProperties()
        {
            var target = new TestWriteOnlyPropertyPoco() { StringValue = "hello", };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestStruct()
        {
            var target = new TestStructPoco { StringValue = "hello", };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            Assert.Equal(target.StringValue, result[nameof(target.StringValue)]?.ToString());
        }

        [Fact]
        public void TestAnonymousType()
        {
            var target = new { Dog1 = "test", Dog2 = "test2", Id = 1 };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);
            Assert.Equal(target.Dog1, result[nameof(target.Dog1)]?.ToString());
            Assert.Equal(target.Dog2, result[nameof(target.Dog2)]?.ToString());
            result[nameof(target.Id)].Should().BeOfType<int>();
            target.Id.Should().Be((int)result[nameof(target.Id)]);
        }

        [Fact]
        public void TestAnonymousTypeEmpty()
        {
            var target = new { };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void TestAnonymousTypeNested()
        {
            var target = new { Name = "Outer", Dog = new { Name = "Inner" }, Cat = new { } };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;
            Assert.NotNull(result);
            result.Should().HaveCount(3);
            result.Should().HaveElementAt(0, new KeyValuePair<string, object>("Name", "Outer"));
            result.ElementAt(1).Key.Should().Be("Dog");
            result.ElementAt(1).Value.Should().BeEquivalentTo(new Dictionary<string, object> { { "Name", "Inner" } });
            result.ElementAt(2).Key.Should().Be("Cat");
            result.ElementAt(2).Value.Should().BeEquivalentTo(new Dictionary<string, object>(0));
        }

        [Fact]
        public void TestAnonymousTypeArray()
        {
            var target = new[] { new { Name = "Anon1" }, new { Name = "Anon2" }, new { Name = "Anon1" } };
            var result = ObjectExtractor.Extract(target) as List<object>;
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result!.ElementAt(0).Should().BeEquivalentTo(new Dictionary<string, object> { { "Name", "Anon1" } });
            result.ElementAt(1).Should().BeEquivalentTo(new Dictionary<string, object> { { "Name", "Anon2" } });
            result.ElementAt(2).Should().BeEquivalentTo(new Dictionary<string, object>(0));
        }

        [Fact]
        public void TestNestedObjectsBelowLimit()
        {
            var target = new TestNestedPropertiesPoco();
            PopulateNestedTarget(target, WafConstants.MaxContainerDepth);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < WafConstants.MaxContainerDepth - 1; i++)
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
            PopulateNestedTarget(target, WafConstants.MaxContainerDepth + 1);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < WafConstants.MaxContainerDepth - 1; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as Dictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestNullList()
        {
            var target = new TestListPoco() { TestList = null };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.Null(items);
        }

        [Fact]
        public void TestListBelowLimit()
        {
            var target = new TestListPoco();
            PopulateListTarget(target, WafConstants.MaxContainerSize);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.Equal(WafConstants.MaxContainerSize, items.Count);

            for (int i = 0; i < WafConstants.MaxContainerSize - 1; i++)
            {
                Assert.Equal($"Prop{i}", items[i]);
            }
        }

        [Fact]
        public void TestListAboveLimit()
        {
            var target = new TestListPoco();
            PopulateListTarget(target, WafConstants.MaxContainerSize + 1);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestList)] as List<object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxContainerSize, items.Count);

            for (int i = 0; i < WafConstants.MaxContainerSize; i++)
            {
                Assert.Equal($"Prop{i}", items[i]);
            }
        }

        [Fact]
        public void TestNullDictionary()
        {
            var target = new TestDictionaryPoco() { TestDictionary = null };

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.Null(items);
        }

        [Fact]
        public void TestDictionaryBelowLimit()
        {
            var target = new TestDictionaryPoco();
            PopulateDictionaryTarget(target, WafConstants.MaxContainerSize);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxContainerSize, items.Count);

            for (int i = 0; i < WafConstants.MaxContainerSize - 1; i++)
            {
                Assert.Equal($"Value{i}", items[$"Prop{i}"]);
            }
        }

        [Fact]
        public void TestDictionaryAboveLimit()
        {
            var target = new TestDictionaryPoco();
            PopulateDictionaryTarget(target, WafConstants.MaxContainerSize + 1);

            var result = ObjectExtractor.Extract(target) as Dictionary<string, object>;

            Assert.NotNull(result);

            var items = result[nameof(target.TestDictionary)] as Dictionary<string, object>;

            Assert.NotNull(items);

            Assert.Equal(WafConstants.MaxContainerSize, items.Count);

            for (int i = 0; i < WafConstants.MaxContainerSize - 1; i++)
            {
                Assert.Equal($"Value{i}", items[$"Prop{i}"]);
            }
        }

        [Fact]
        public void TestCyclicObjects()
        {
            var target = new TestNestedPropertiesPoco();
            var linker = new TestNestedPropertiesPoco() { TestNestedPropertiesPocoValue = target };

            target.TestNestedPropertiesPocoValue = linker;

            var result = ObjectExtractor.Extract(target) as IReadOnlyDictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                result = result[nameof(target.TestNestedPropertiesPocoValue)] as IReadOnlyDictionary<string, object>;
                Assert.NotNull(result);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestCyclicList()
        {
            var target = new TestNestedListPoco();
            var linker = new TestNestedListPoco() { TestList = new List<TestNestedListPoco> { target } };

            target.TestList.Add(linker);

            var result = ObjectExtractor.Extract(target) as IReadOnlyDictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                var items = result[nameof(target.TestList)] as List<object>;
                Assert.NotEmpty(items);
                result = items[0] as IReadOnlyDictionary<string, object>;
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestCyclicDictionary()
        {
            const string linkKey = "next";
            var target = new TestNestedDictionaryPoco();
            var linker = new TestNestedDictionaryPoco() { TestDictionary = new Dictionary<string, TestNestedDictionaryPoco> { { linkKey, target } } };

            target.TestDictionary.Add(linkKey, linker);

            var result = ObjectExtractor.Extract(target) as IReadOnlyDictionary<string, object>;

            Assert.NotNull(result);

            for (int i = 0; i < 2; i++)
            {
                var items = result[nameof(target.TestDictionary)] as IReadOnlyDictionary<string, object>;
                Assert.NotEmpty(items);
                result = items[linkKey] as IReadOnlyDictionary<string, object>;
            }

            Assert.Empty(result);
        }

        [Fact]
        public void TestNullValues()
        {
            TestObject testObject = new TestObject();
            testObject.Test = new();
            var result = ObjectExtractor.Extract(testObject);
            result.Should().NotBeNull();
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

#if NET6_0_OR_GREATER
        public DateOnly DateOnlyValue { get; set; }

        public TimeOnly TimeOnlyValue { get; set; }

#endif
        public DateTimeOffset DateTimeOffsetValue { get; set; }

        public TimeSpan TimeSpanValue { get; set; }

        public string StringValue { get; set; }

        public EnumValues EnumValue { get; set; }

        public enum EnumValues
        {
#pragma warning disable SA1602 // Enumeration items should be documented
            Value1,
            Value2,
            Value3
#pragma warning restore SA1602 // Enumeration items should be documented
        }
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

    public class TestObject
    {
        public TestObject Test { get; set; }

        public object Prop { get; set; }

        public override int GetHashCode()
        {
            return Test.GetHashCode() + Prop.GetHashCode();
        }
    }
}
