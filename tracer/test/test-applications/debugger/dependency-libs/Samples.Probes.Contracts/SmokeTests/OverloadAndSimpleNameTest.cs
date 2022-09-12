// <copyright file="OverloadAndSimpleNameTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    public class OverloadAndSimpleNameTest : IRun
    {
        public void Run()
        {
            // Call every single "Method", while a single Method Probe request is applied.
            // Take a note that the signature is not given, thus it shall instrument every single overload.

            Method("First Call"); // Should be instrumented.
            CaptureException(() => Method(int.MinValue)); // Should be instrumented.
            CaptureException(() => Method(int.MinValue, int.MaxValue)); // Should be instrumented.
            CaptureException(() => Method('@')); // Should be instrumented.

            var p1 = new Person("Theodor Herzl", 44, new Address(), new System.Guid(), null);
            var p2 = new Person("David Wolffsohn", 58, new Address(), new System.Guid(), null);
            new InnerType().Method(p1, p2); // Should not be instrumented. The method name matches, but the class name does not
            new InnerType().Method("Marty McFly"); // Should not be instrumented. The method name match. but the class name does not.

            // As inner type (in global namespace):
            new OuterType.OverloadAndSimpleNameTest().Method(p1, p2); // Should be instrumented.
            new OuterType.OverloadAndSimpleNameTest().Method("Marty McFly"); // Should be instrumented.

            // As inner type in another namespace:
            new Spicing.Things.Down.OuterType.OverloadAndSimpleNameTest().Method('@'); // Should be instrumented.

            // As Another namespace:
            new Spicing.Things.Up.OverloadAndSimpleNameTest().Method(new Address { City = new Place { Name = "Some Place" } }); // Should be instrumented.
            new Spicing.Things.Up.OverloadAndSimpleNameTest().Method('@'); // Should be instrumented.
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(useFullTypeName: false, unlisted: true)]
        public void Method(string callerName)
        {
            int a = callerName.Length;
            a++;
            a++;
            a++;
        }

        public char Method(int a, int b)
        {
            return ThrowException<char>(null);
        }

        public string Method(int a)
        {
            return ThrowException<string>(null);
        }

        public void Method(char c)
        {
            var local = nameof(Method) + c;
            ThrowException<int>(local);
        }

        public void CaptureException(Action callback)
        {
            try
            {
                callback();
            }
            catch
            {
                // Ignored
            }
        }

        private T ThrowException<T>(string local)
        {
            throw new System.NotImplementedException();
        }

        internal class InnerType
        {
            public Person Method(Person a, Person b)
            {
                var isEqual = a.Equals(b);
                return new Person("Person Name", isEqual ? 0 : 50.50, new Address(), new System.Guid(), null);
            }

            public string Method(string a)
            {
                return nameof(Method) + a;
            }
        }
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1403 // File may only contain a single namespace

namespace Spicing.Things.Up
{
    internal class OverloadAndSimpleNameTest
    {
        public Address Method(Address adr)
        {
            return adr;
        }

        public string Method(char c)
        {
            return typeof(OverloadAndSimpleNameTest).FullName + "." + nameof(Method) + "." + c;
        }
    }
}

namespace Spicing.Things.Down
{
    internal class OuterType
    {
        public class OverloadAndSimpleNameTest
        {
            public string Method(char c)
            {
                return typeof(OverloadAndSimpleNameTest).FullName + "." + nameof(Method) + "." + c;
            }
        }
    }
}

internal class OuterType
{
    public class OverloadAndSimpleNameTest
    {
        public Person Method(Person a, Person b)
        {
            var isEqual = a.Equals(b);
            return new Person("Person Name", isEqual ? 0 : 50.50, new Address(), new System.Guid(), null);
        }

        public string Method(string a)
        {
            return typeof(OverloadAndSimpleNameTest).FullName + "." + nameof(Method) + "[" + a + "]";
        }
    }
}

#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1403 // File may only contain a single namespace
