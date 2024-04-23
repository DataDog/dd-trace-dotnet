// <copyright file="JavaScriptSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Web.Script.Serialization;
#endif
using System;
using System.Collections.Generic;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Json;

public class JavaScriptSerializerTests : InstrumentationTestsBase
{
    private readonly string _taintedJson = "{ \"key\": \"value\" }";
    private readonly string _taintedJsonMultiple = "{\"key1\": \"value1\", \"key2\": \"value2\"}";
    private readonly string _taintedJsonArray = "[\"value1\", \"value2\"]";
    private readonly string _taintedJsonDeepObject = "{\"key\": {\"key2\": \"value\"}}";
    private readonly string _taintedJsonDifferentTypes = "{ \"name\": \"Chris\", \"age\": 23, \"address\": { \"city\": \"New York\", \"country\": \"America\" }, \"friends\": [ { \"name\": \"Emily\", \"hobbies\": [ \"biking\", \"music\", \"gaming\" ] }, { \"name\": \"John\", \"hobbies\": [ \"soccer\", \"gaming\" ] }, [ \"aString\", { \"obj\": \"val\" } ] ] }";

#if NETFRAMEWORK
    private readonly string _notTaintedJson = "{ \"key\": \"notTainted\" }";
#endif

    public JavaScriptSerializerTests()
    {
        AddTainted(_taintedJson);
        AddTainted(_taintedJsonMultiple);
        AddTainted(_taintedJsonArray);
        AddTainted(_taintedJsonDeepObject);
        AddTainted(_taintedJsonDifferentTypes);
    }
    
#if NETFRAMEWORK
    [Fact]
    public void DeserializeObject_WithTaintedJson_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(_taintedJson);
        var value = obj["key"];
        
        Assert.Equal("value", value);
        AssertTainted(value);
    }
    
    [Fact]
    public void DeserializeObject_WithTaintedInputCraftedJson_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var input = "value";
        AddTainted(input);
        
        var json = @"{ ""cmd"": """ + input + @""", ""arg"": ""arg1"" }";
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(json);
        var cmd = obj["cmd"] as string;
        var arg = obj["arg"] as string;
        
        Assert.Equal("value", cmd);
        Assert.Equal("arg1", arg);
        AssertTainted(cmd);
        AssertTainted(arg);
    }
    
    [Fact]
    public void DeserializeObject_WithTaintedJsonMultiple_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(_taintedJsonMultiple);
        var value1 = obj["key1"];
        var value2 = obj["key2"];
        
        Assert.Equal("value1", value1);
        AssertTainted(value1);
        Assert.Equal("value2", value2);
        AssertTainted(value2);
    }
    
    [Fact]
    public void DeserializeObject_WithTaintedJsonArray_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (object[])serializer.DeserializeObject(_taintedJsonArray);
        var value1 = obj[0];
        var value2 = obj[1];
        
        Assert.Equal("value1", value1);
        AssertTainted(value1);
        Assert.Equal("value2", value2);
        AssertTainted(value2);
    }
    
    [Fact]
    public void DeserializeObject_WithTaintedJsonDeepObject_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(_taintedJsonDeepObject);
        var value = ((Dictionary<string, object>)obj["key"])["key2"];
        
        Assert.Equal("value", value);
        AssertTainted(value);
    }
    
    [Fact]
    public void DeserializeObject_WithTaintedJsonDifferentTypes_ShouldBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(_taintedJsonDifferentTypes);
        var name = obj["name"];
        var age = obj["age"];
        var address = (Dictionary<string, object>)obj["address"];
        var city = address["city"];
        var country = address["country"];
        var friends = (object[])obj["friends"];
        var friend1 = (Dictionary<string, object>)friends[0];
        var friend1Name = friend1["name"];
        var friend1Hobbies = (object[])friend1["hobbies"];
        var friend1Hobby1 = friend1Hobbies[0];
        var friend1Hobby2 = friend1Hobbies[1];
        var friend1Hobby3 = friend1Hobbies[2];
        var friend2 = (Dictionary<string, object>)friends[1];
        var friend2Name = friend2["name"];
        var friend2Hobbies = (object[])friend2["hobbies"];
        var friend2Hobby1 = friend2Hobbies[0];
        var friend2Hobby2 = friend2Hobbies[1];
        var friend3 = (object[])friends[2];
        var friend3String = friend3[0];
        var friend3Obj = (Dictionary<string, object>)friend3[1];
        var friend3ObjValue = friend3Obj["obj"];
        
        Assert.Equal("Chris", name);
        AssertTainted(name);
        Assert.Equal("New York", city);
        AssertTainted(city);
        Assert.Equal("America", country);
        AssertTainted(country);
        Assert.Equal("Emily", friend1Name);
        AssertTainted(friend1Name);
        Assert.Equal("biking", friend1Hobby1);
        AssertTainted(friend1Hobby1);
        Assert.Equal("music", friend1Hobby2);
        AssertTainted(friend1Hobby2);
        Assert.Equal("gaming", friend1Hobby3);
        AssertTainted(friend1Hobby3);
        Assert.Equal("John", friend2Name);
        AssertTainted(friend2Name);
        Assert.Equal("soccer", friend2Hobby1);
        AssertTainted(friend2Hobby1);
        Assert.Equal("gaming", friend2Hobby2);
        AssertTainted(friend2Hobby2);
        Assert.Equal("aString", friend3String);
        AssertTainted(friend3String);
        Assert.Equal("val", friend3ObjValue);
        AssertTainted(friend3ObjValue);
    }
    
    [Fact]
    public void DeserializeObject_WithNotTaintedJson_ShouldNotBeTainted()
    {
        var serializer = new JavaScriptSerializer();
        var obj = (Dictionary<string, object>)serializer.DeserializeObject(_notTaintedJson);
        var value = obj["key"];
        
        Assert.Equal("notTainted", value);
        AssertNotTainted(value);
    }
#endif
}

