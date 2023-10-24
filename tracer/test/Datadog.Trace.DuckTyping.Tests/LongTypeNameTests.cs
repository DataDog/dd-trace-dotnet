// <copyright file="LongTypeNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1403 // file may only contain one namespace

namespace Datadog.Trace.DuckTyping.Tests
{
    public class LongTypeNameTests
    {
        public const string ExpectedValue = "It works!";

        [Fact]
        public void CanDuckTypeLongNamedTypes()
        {
            // We saw this in an escalation
            // Type name was too long. The fully qualified type name must be less than 1,024 characters
            // Note that this limitation doesn't include the length of any parent classes for nested types
            // or the length of the assembly name
            const int maxTypeNameSize = 1024;

            var originalTestCase = new Grpc.AspNetCore.Server.Internal.CallHandlers.UnaryServerCallHandler<
                                       SomeNamespace.ThatIsPrettyLong.Ish.Services.ProductSecretValuesServiceImpl<
                                           SomeNamespace.ThatIsPrettyLong.Ish.Data.Infrastructure.Database.Connection.DatabaseConnectionHandle,
                                           SomeNamespace.ThatIsPrettyLong.Ish.Data.Infrastructure.Database.Transaction.DatabaseTransactionHandle>,
                                        SomeNamespace.ThatIsPrettyLong.Ish.Services.SomeBigProductThing.GetSuperSecretWossnameToHandleThisNewRequest,
                                        SomeNamespace.ThatIsPrettyLong.Ish.Services.SomeBigProductThing.ThisTestWossnameResponse>();

            var longGeneric = new MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                    MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                        MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                            MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                    MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                        MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                            MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                    MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                        MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                            MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                                MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<
                                                                    MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<string>>>>>>>>>>>>>>>();

            // We can't create a type which has a "raw" name longer than 1024 (doesn't compile)
            // But this will mean will still need to do some truncation when creating the type
            var javaStyle = new CreatingSomeNestingToMakeThingsEvenWorse.MyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName();

            longGeneric.GetType().FullName!.Length.Should().BeGreaterThan(maxTypeNameSize);
            originalTestCase.GetType().FullName!.Length.Should().BeGreaterThan(maxTypeNameSize);

            // original test case
            var interfaceProxy = originalTestCase.DuckCast<ILongNamedTypeProxy>();
            interfaceProxy.Value.Should().Be(ExpectedValue);

            var structProxy = longGeneric.DuckCast<LongNamedTypeProxy>();
            structProxy.Value.Should().Be(ExpectedValue);

            // long generic
            interfaceProxy = longGeneric.DuckCast<ILongNamedTypeProxy>();
            interfaceProxy.Value.Should().Be(ExpectedValue);

            structProxy = longGeneric.DuckCast<LongNamedTypeProxy>();
            structProxy.Value.Should().Be(ExpectedValue);

            // javaStyle
            interfaceProxy = javaStyle.DuckCast<ILongNamedTypeProxy>();
            interfaceProxy.Value.Should().Be(ExpectedValue);

            structProxy = javaStyle.DuckCast<LongNamedTypeProxy>();
            structProxy.Value.Should().Be(ExpectedValue);
        }

        public class MyTargetTypeThatHasAReallyReallyReallyLongNameAndIsGenericToMakeItWorse<T>
        {
            public string Value => ExpectedValue;
        }

        public class CreatingSomeNestingToMakeThingsEvenWorse
        {
            public class MyTargetTypeThatHasAReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyReallyLongName
            {
                public string Value => ExpectedValue;
            }
        }

        public interface ILongNamedTypeProxy
        {
            public string Value { get; }
        }

        [DuckCopy]
        public struct LongNamedTypeProxy
        {
            public string Value;
        }
    }
}

namespace Grpc.AspNetCore.Server.Internal.CallHandlers
{
    public class UnaryServerCallHandler<T1, T2, T3>
    {
        public string Value => Datadog.Trace.DuckTyping.Tests.LongTypeNameTests.ExpectedValue;
    }
}

namespace SomeNamespace.ThatIsPrettyLong.Ish
{
    namespace Services
    {
        public class ProductSecretValuesServiceImpl<T1, T2>
        {
        }

        namespace SomeBigProductThing
        {
            public class GetSuperSecretWossnameToHandleThisNewRequest
            {
            }

            public class ThisTestWossnameResponse
            {
            }
        }
    }

    namespace Data.Infrastructure.Database
    {
        public class Connection
        {
            public class DatabaseConnectionHandle
            {
            }
        }

        public class Transaction
        {
            public class DatabaseTransactionHandle
            {
            }
        }
    }
}
