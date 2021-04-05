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
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            var @delegate = DelegatesHelper.GetInternalProcessExitDelegate();
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            Assert.NotNull(@delegate);

            void CurrentDomain_ProcessExit(object sender, EventArgs e)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void GetCancelKeyPressDelegateTest()
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            var @delegate = DelegatesHelper.GetInternalCancelKeyPressDelegate();
            Console.CancelKeyPress -= Console_CancelKeyPress;
            Assert.NotNull(@delegate);

            void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void SetLastDelegateTest()
        {
            var lstEvents = new List<int>();
            var targetObject = new TargetObject();

            targetObject.MyEvent += TargetObject_MyEvent;
            targetObject.MyEvent += TargetObject_MyEvent2;
            targetObject.FireEvent();
            targetObject.MyEvent -= TargetObject_MyEvent;
            targetObject.MyEvent -= TargetObject_MyEvent2;

            Assert.Equal(1, lstEvents[0]);
            Assert.Equal(2, lstEvents[1]);
            Assert.Equal(3, lstEvents[2]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = targetObject.GetDelegate();

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
            var targetObject = new TargetObject();

            targetObject.MyEvent += TargetObject_MyEvent2;
            targetObject.MyEvent += TargetObject_MyEvent;
            targetObject.FireEvent();
            targetObject.MyEvent -= TargetObject_MyEvent2;
            targetObject.MyEvent -= TargetObject_MyEvent;

            Assert.Equal(2, lstEvents[0]);
            Assert.Equal(1, lstEvents[1]);
            Assert.Equal(3, lstEvents[2]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = targetObject.GetDelegate();

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
        public void SetLastDelegate3HandlersTest()
        {
            var lstEvents = new List<int>();
            var targetObject = new TargetObject();

            targetObject.MyEvent += TargetObject_MyEvent2;
            targetObject.MyEvent += TargetObject_MyEvent;
            targetObject.MyEvent += TargetObject_MyEvent2;
            targetObject.FireEvent();
            targetObject.MyEvent -= TargetObject_MyEvent2;
            targetObject.MyEvent -= TargetObject_MyEvent;
            targetObject.MyEvent -= TargetObject_MyEvent2;

            Assert.Equal(2, lstEvents[0]);
            Assert.Equal(1, lstEvents[1]);
            Assert.Equal(2, lstEvents[2]);
            Assert.Equal(3, lstEvents[3]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = targetObject.GetDelegate();

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
        public void DuplicateDelegateTest()
        {
            var lstEvents = new List<int>();
            var targetObject = new TargetObject();

            targetObject.MyEvent += TargetObject_MyEvent;
            targetObject.FireEvent();
            targetObject.MyEvent -= TargetObject_MyEvent;

            Assert.Equal(1, lstEvents[0]);
            Assert.Equal(2, lstEvents[1]);

            void TargetObject_MyEvent(object sender, EventArgs e)
            {
                lstEvents.Add(1);

                var targetDelegate = targetObject.GetDelegate();

                if (!DelegatesHelper.TrySetLastDelegate(targetDelegate, new EventHandler(TargetObject_MyEvent)))
                {
                    lstEvents.Add(2);
                }
            }
        }

        internal class TargetObject
        {
            public event EventHandler MyEvent;

            public EventHandler GetDelegate() => MyEvent;

            public void FireEvent()
            {
                MyEvent?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
