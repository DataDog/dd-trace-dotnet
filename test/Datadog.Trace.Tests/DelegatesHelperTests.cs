using System;
using System.Collections.Generic;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class DelegatesHelperTests
    {
        [Fact]
        public void GetProcessExitDelegateTest()
        {
            var @delegate = DelegatesHelper.GetInternalProcessExitDelegate();
            Assert.NotNull(@delegate);
        }

        [Fact]
        public void SetLastDelegateTest()
        {
            var lstEvents = new List<int>();

            TargetObject.MyEvent += TargetObject_MyEvent;
            TargetObject.MyEvent += TargetObject_MyEvent2;
            TargetObject.FireEvent();
            TargetObject.MyEvent -= TargetObject_MyEvent;
            TargetObject.MyEvent -= TargetObject_MyEvent2;

            Assert.Equal(1, lstEvents[0]);
            Assert.Equal(2, lstEvents[1]);
            Assert.Equal(3, lstEvents[2]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = TargetObject.GetDelegate();

                var lastDelegate = new EventHandler((s, eArgs) =>
                {
                    lstEvents.Add(3);
                });

                if (!DelegatesHelper.TrySetLastDelegate(targetDelegate, lastDelegate))
                {
                    lastDelegate(sender, e);
                }
            }

            void TargetObject_MyEvent2(object sender, EventArgs e) => lstEvents.Add(2);
        }

        [Fact]
        public void SetLastDelegateInversedTest()
        {
            var lstEvents = new List<int>();

            TargetObject.MyEvent += TargetObject_MyEvent2;
            TargetObject.MyEvent += TargetObject_MyEvent;
            TargetObject.FireEvent();
            TargetObject.MyEvent -= TargetObject_MyEvent2;
            TargetObject.MyEvent -= TargetObject_MyEvent;

            Assert.Equal(2, lstEvents[0]);
            Assert.Equal(1, lstEvents[1]);
            Assert.Equal(3, lstEvents[2]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = TargetObject.GetDelegate();

                var lastDelegate = new EventHandler((s, eArgs) =>
                {
                    lstEvents.Add(3);
                });

                if (!DelegatesHelper.TrySetLastDelegate(targetDelegate, lastDelegate))
                {
                    lastDelegate(sender, e);
                }
            }

            void TargetObject_MyEvent2(object sender, EventArgs e) => lstEvents.Add(2);
        }

        [Fact]
        public void SetLastDelegaten3HandlersTest()
        {
            var lstEvents = new List<int>();

            TargetObject.MyEvent += TargetObject_MyEvent2;
            TargetObject.MyEvent += TargetObject_MyEvent;
            TargetObject.MyEvent += TargetObject_MyEvent2;
            TargetObject.FireEvent();
            TargetObject.MyEvent -= TargetObject_MyEvent2;
            TargetObject.MyEvent -= TargetObject_MyEvent;
            TargetObject.MyEvent -= TargetObject_MyEvent2;

            Assert.Equal(2, lstEvents[0]);
            Assert.Equal(1, lstEvents[1]);
            Assert.Equal(2, lstEvents[2]);
            Assert.Equal(3, lstEvents[3]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = TargetObject.GetDelegate();

                var lastDelegate = new EventHandler((s, eArgs) =>
                {
                    lstEvents.Add(3);
                });

                if (!DelegatesHelper.TrySetLastDelegate(targetDelegate, lastDelegate))
                {
                    lastDelegate(sender, e);
                }
            }

            void TargetObject_MyEvent2(object sender, EventArgs e) => lstEvents.Add(2);
        }

        internal static class TargetObject
        {
            public static event EventHandler MyEvent;

            public static EventHandler GetDelegate() => MyEvent;

            public static void FireEvent()
            {
                MyEvent?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
