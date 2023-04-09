using System;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(useFullTypeName: false, unlisted: true, templateStr: "We cannot use a template with interpolated expressions here, because this is a probe on an  overloaded method, and that is currently not supported.")]
        public void Method(string callerName)
        {
            int a = callerName.Length;
            a++;
            if (a > 5)
            {
                a++;
            }
            else
            {
                a += 2;
            }

            Console.WriteLine(a);
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

        private T ThrowException<T>(string local)
        {
            throw new System.NotImplementedException();
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

        class InnerType
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

class OuterType
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

namespace Spicing.Things.Up
{
    class OverloadAndSimpleNameTest
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
    class OuterType
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
