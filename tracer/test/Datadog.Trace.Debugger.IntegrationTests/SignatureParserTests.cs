// <copyright file="SignatureParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Debugger.Models;
using FluentAssertions;
using Samples.Probes.TestRuns.SmokeTests;
using Xunit;

namespace Datadog.Trace.Debugger.IntegrationTests
{
    public class SignatureParserTests
    {
        [Theory]
        [InlineData("(P<T<V,K>>, K<T<V>, P<K>>)", new[] { "P[T[V,K]]", "K[T[V],P[K]]" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1,WebService.Controllers.Animal`1<!!0>,WebService.Controllers.Animal`1<!0>,WebService.Controllers.Animal`1<!!1>", new[] { "WebService.Controllers.MeGenericClass1`1", "WebService.Controllers.Animal`1[!!0]", "WebService.Controllers.Animal`1[!0]", "WebService.Controllers.Animal`1[!!1]" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1+MeInnerGeneric`4,WebService.Controllers.MultiAnimal`6<!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1<!!1>>,WebService.Controllers.Animal`1<!!2>", new[] { "WebService.Controllers.MeGenericClass1`1+MeInnerGeneric`4", "WebService.Controllers.MultiAnimal`6[!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1[!!1]]", "WebService.Controllers.Animal`1[!!2]" })]
        [InlineData("(WebService.Repositories.RoomRepository+<>c__DisplayClass7_0, WebService.Models.Room)", new[] { "WebService.Repositories.RoomRepository+<>c__DisplayClass7_0", "WebService.Models.Room" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1,!0", new[] { "WebService.Controllers.MeGenericClass1`1", "!0" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1+MeInnerGeneric2`4,WebService.Controllers.MultiAnimal`6<!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1<!!1>>,WebService.Controllers.Animal`1<!!2>", new[] { "WebService.Controllers.MeGenericClass1`1+MeInnerGeneric2`4", "WebService.Controllers.MultiAnimal`6[!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1[!!1]]", "WebService.Controllers.Animal`1[!!2]" })]
        [InlineData("WebService.Controllers.MeGenericClass2`2,!0", new[] { "WebService.Controllers.MeGenericClass2`2", "!0" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1,!0,!!0", new[] { "WebService.Controllers.MeGenericClass1`1", "!0", "!!0" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1,WebService.Controllers.Animal`1<!0>", new[] { "WebService.Controllers.MeGenericClass1`1", "WebService.Controllers.Animal`1[!0]" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1+MeInnerGeneric2`4,WebService.Controllers.MultiAnimal`6<!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1<!!1>>", new[] { "WebService.Controllers.MeGenericClass1`1+MeInnerGeneric2`4", "WebService.Controllers.MultiAnimal`6[!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1[!!1]]" })]
        [InlineData("(WebService.Controllers.RoomsController, System.String, System.String)", new[] { "WebService.Controllers.RoomsController", "System.String", "System.String" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1,WebService.Controllers.Animal`1<WebService.Controllers.P>,!!0,!!1,!!2,!!3", new[] { "WebService.Controllers.MeGenericClass1`1", "WebService.Controllers.Animal`1[WebService.Controllers.P]", "!!0", "!!1", "!!2", "!!3" })]
        [InlineData("()", null)]
        [InlineData("WebService.Controllers.MeGenericClass1`1,WebService.Controllers.Animal`1<WebService.Controllers.P>", new[] { "WebService.Controllers.MeGenericClass1`1", "WebService.Controllers.Animal`1[WebService.Controllers.P]" })]
        [InlineData("(WebService.Controllers.A, WebService.Controllers.B, WebService.Controllers.C)", new[] { "WebService.Controllers.A", "WebService.Controllers.B", "WebService.Controllers.C" })]
        [InlineData("WebService.Controllers.GenericClass`1<GenericType>", new[] { "WebService.Controllers.GenericClass`1[GenericType]" })]
        [InlineData("SimpleClass", new[] { "SimpleClass" })]
        [InlineData("GenericClass`2<T1,T2>", new[] { "GenericClass`2[T1,T2]" })]
        [InlineData("Outer`1<Inner`2<T1,T2>>", new[] { "Outer`1[Inner`2[T1,T2]]" })]
        [InlineData("Namespace.Class+Nested`1<Namespace.Generic`2<Arg1,Arg2>>", new[] { "Namespace.Class+Nested`1[Namespace.Generic`2[Arg1,Arg2]]" })]
        [InlineData("ClassWith1Generic`1<T1>,ClassWith2Generics`2<T1,T2>,ClassWith3Generics`3<T1,T2,T3>", new[] { "ClassWith1Generic`1[T1]", "ClassWith2Generics`2[T1,T2]", "ClassWith3Generics`3[T1,T2,T3]" })]
        [InlineData("A,B,C,D,E,F,G", new[] { "A", "B", "C", "D", "E", "F", "G" })]
        [InlineData("(A, B, C, D, E, F, G)", new[] { "A", "B", "C", "D", "E", "F", "G" })]
        [InlineData("(InvalidType<)", null)]
        [InlineData("InvalidType>)", null)]
        [InlineData("(InvalidType>)", null)]
        [InlineData("(A, InvalidType<)", null)]
        [InlineData("(ValidType<>)", new[] { "ValidType<>" })]
        [InlineData("WebService.Controllers.MeGenericClass1`1+MeInnerGeneric2`4<WebService.Controllers.MultiAnimal`6<!1,!2,!4,!!0,!!1,WebService.Controllers.Animal`1<!!1>>", null)]
        [InlineData("Namespace1.Class1`1<Namespace2.Class2`2<Arg1, Arg2>, Namespace3.Class3>", new[] { "Namespace1.Class1`1[Namespace2.Class2`2[Arg1,Arg2],Namespace3.Class3]" })]
        [InlineData("OuterNamespace.OuterClass`1<InnerNamespace1.InnerClass`2<Arg1,Arg2>,InnerNamespace2.InnerClass>", new[] { "OuterNamespace.OuterClass`1[InnerNamespace1.InnerClass`2[Arg1,Arg2],InnerNamespace2.InnerClass]" })]
        [InlineData("A.B.C.D`1<A.B.C.E`2<A.B.C.F,A.B.C.G>>", new[] { "A.B.C.D`1[A.B.C.E`2[A.B.C.F,A.B.C.G]]" })]
        [InlineData("ClassWithGeneric`1<AnotherClassWithGeneric`2<One,Two>,ThirdClass>", new[] { "ClassWithGeneric`1[AnotherClassWithGeneric`2[One,Two],ThirdClass]" })]
        [InlineData("Namespace1.Class1`1<Namespace2.Class2`1<Namespace3.Class3>>", new[] { "Namespace1.Class1`1[Namespace2.Class2`1[Namespace3.Class3]]" })]
        [InlineData("(Outer`1<Inner`2<T1,T2>>)", new[] { "Outer`1[Inner`2[T1,T2]]" })]
        [InlineData("(Outer`1, Inner`2<T1>, T2>", null)]
        [InlineData("OuterClass<InnerClass1>, A<<InnerClass2>", null)]
        [InlineData("OuterClass<InnerClass1>, A<InnerClass2>", new[] { "OuterClass[InnerClass1]", "A[InnerClass2]" })]
        [InlineData("(OuterClass<InnerClass1>, A<<InnerClass2>)", null)]
        [InlineData("(OuterClass<InnerClass1>, A<InnerClass2>)", new[] { "OuterClass[InnerClass1]", "A[InnerClass2]" })]
        [InlineData("Namespace1.Class1`1<Namespace2.Class2`1<Namespace3.Class3>", null)]
        [InlineData("OuterClass<InnerClass1,InnerClass2>", new[] { "OuterClass[InnerClass1,InnerClass2]" })]
        [InlineData("Class1<Class2<Class3<Class4>>>", new[] { "Class1[Class2[Class3[Class4]]]" })]
        [InlineData("(Namespace1.Class1`1<Namespace2.Class2`1<Namespace3.Class3>)", null)]
        [InlineData("Namespace1.Class1`1<Namespace2.<PrivateImplementationDetails>, Namespace3.Class3>", new[] { "Namespace1.Class1`1[Namespace2.[PrivateImplementationDetails],Namespace3.Class3]" })]
        [InlineData("Namespace1.Class1`1<Namespace2.<>z__ReadOnlyArray<T>, Namespace3.Class3>", new[] { "Namespace1.Class1`1[Namespace2.<>z__ReadOnlyArray[T],Namespace3.Class3]" })]
        public void ParseSignaturesTest(string signature, string[] expectedArgs)
        {
            var receivedArgs = SignatureParser.Parse(signature);
            receivedArgs.Should().BeEquivalentTo(expectedArgs, options => options.WithStrictOrdering());
        }
    }
}
