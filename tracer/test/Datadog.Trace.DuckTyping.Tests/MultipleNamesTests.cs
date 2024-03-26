// <copyright file="MultipleNamesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Local

using FluentAssertions;
using Xunit;

#pragma warning disable CS0649
#pragma warning disable CS0169
#pragma warning disable SA1401
#pragma warning disable SA1201

namespace Datadog.Trace.DuckTyping.Tests;

public class MultipleNamesTests
{
    [Fact]
    public void FieldNameTest()
    {
        var objA = new FieldObjectA();
        var objB = new FieldObjectB();

        var duckTypeA = objA.DuckCast<IFieldObjects>();
        var duckTypeB = objB.DuckCast<IFieldObjects>();

        const string objAString = "ObjectA field name";
        const string objBString = "ObjectB field name";

        duckTypeA.FieldName = objAString;
        duckTypeB.FieldName = objBString;

        Assert.Equal(objAString, duckTypeA.FieldName);
        Assert.Equal(objBString, duckTypeB.FieldName);
    }

    [Fact]
    public void PropertyNameTest()
    {
        var objA = new PropertyObjectA();
        var objB = new PropertyObjectB();

        var duckTypeA = objA.DuckCast<IPropertyObjects>();
        var duckTypeB = objB.DuckCast<IPropertyObjects>();

        const string objAString = "ObjectA property name";
        const string objBString = "ObjectB property name";

        duckTypeA.PropertyName = objAString;
        duckTypeB.PropertyName = objBString;

        Assert.Equal(objAString, objA.PropertyName1);
        Assert.Equal(objBString, objB.PropertyName2);

        Assert.Equal(objAString, duckTypeA.PropertyName);
        Assert.Equal(objBString, duckTypeB.PropertyName);
    }

    [Fact]
    public void SameObjectFieldNameTest()
    {
        var objA = new WithMultipleFieldsObject();

        var duckTypeA = objA.DuckCast<IFieldObjects>();

        const string objAString = "ObjectA field name";

        duckTypeA.FieldName = objAString;

        Assert.Equal(objAString, objA._fieldName1);
        Assert.Null(objA._fieldName2);

        Assert.Equal(objAString, duckTypeA.FieldName);
    }

    [Fact]
    public void ReverseProxyWithPropertiesTest()
    {
        var instance = new ReverseProxyWithProperties();
        var proxy1 = (IReverseProxyWithPropertiesTest1)instance.DuckImplement(typeof(IReverseProxyWithPropertiesTest1));
        var proxy2 = (IReverseProxyWithPropertiesTest2)instance.DuckImplement(typeof(IReverseProxyWithPropertiesTest2));

        proxy1.Value1.Should().Be(instance.Value);
        proxy2.Value2.Should().Be(instance.Value);

        proxy1.Value1 = "Modified by Proxy 1";
        proxy1.Value1.Should().Be(instance.Value);
        proxy2.Value2.Should().Be(instance.Value);

        proxy1.Value1 = "Modified by Proxy 2";
        proxy1.Value1.Should().Be(instance.Value);
        proxy2.Value2.Should().Be(instance.Value);
    }

    private interface IFieldObjects
    {
        [DuckField(Name = "_fieldName1,_fieldName2")]
        string FieldName { get; set; }
    }

    private interface IPropertyObjects
    {
        [Duck(Name = "PropertyName1,PropertyName2")]
        string PropertyName { get; set; }
    }

    private class FieldObjectA
    {
        private string _fieldName1;
    }

    private class FieldObjectB
    {
        private string _fieldName2;
    }

    private class PropertyObjectA
    {
        public string PropertyName1 { get; set; }
    }

    private class PropertyObjectB
    {
        public string PropertyName2 { get; set; }
    }

    private class WithMultipleFieldsObject
    {
        public string _fieldName1;
        public string _fieldName2;
    }

    private interface IReverseProxyWithPropertiesTest1
    {
        string Value1 { get; set; }
    }

    private interface IReverseProxyWithPropertiesTest2
    {
        string Value2 { get; set; }
    }

    public class ReverseProxyWithProperties
    {
        [DuckReverseMethod(Name = "Value1,Value2")]
        public string Value { get; set; } = "Datadog";
    }
}
