// <copyright file="GenericTypes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SA1314 // Type parameter names should begin with T  | need to make the difference between generic types
#pragma warning disable SA1402 // File may only contain a single type       | contains alls test types
#pragma warning disable SA1649 // File name should match first type name    | contains alls test types
namespace Datadog.Demos.Computer01
{
    public struct ValueType
    {
        private int _value;

        public ValueType(int val)
        {
            _value = val;
        }

        public int Get() => _value;
    }

    /// <summary>
    /// Create callstacks with methods exposed by generic types covering:
    ///     - basic types such as bool
    ///     - reference types and value types
    ///     - types defined in the same assembly
    ///     - types defined in another assembly
    /// </summary>
    public class GenericTypes
    {
        private const int SleepDurationMs = 0;

        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);
            _activeTasks = new List<Task>
            {
                Task.Factory.StartNew(DoBaseTypes, TaskCreationOptions.LongRunning),
                Task.Factory.StartNew(DoValueAndReferenceTypes, TaskCreationOptions.LongRunning),

                // TODO: find a way to get a callstack with methods of generic types from other assemblies
                //       to ensure this case is covered (BTW, should work because the module is also
                //       returned by GetFunctionInfo2
            };
        }

        public void Stop()
        {
            if (_stopEvent == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _stopEvent.Set();

            Task.WhenAll(_activeTasks).Wait();

            _stopEvent.Dispose();
            _stopEvent = null;
            _activeTasks = null;
        }

        public void Run()
        {
            var numbers = Enumerable.Range(0, 127);
            RunBaseTypes(numbers);
            RunValueAndReferenceTypes(numbers);
        }

        // check
        // + long generic parameter types list with basic types
        // + reference type in generic parameter types
        private void DoBaseTypes()
        {
            Console.WriteLine($"Starting {nameof(DoBaseTypes)}.");

            var numbers = Enumerable.Range(0, 127);
            while (!_stopEvent.WaitOne(SleepDurationMs))
            {
                RunBaseTypes(numbers);
            }

            Console.WriteLine($"Exiting {nameof(DoBaseTypes)}.");
        }

        // if a generic type parameter is a reference type, it becomes impossible to get it
        // from ICorProfilerInfo and need to use the IMedataDataImport API to list the
        // un-instanciated definition of the generic type
        private void DoValueAndReferenceTypes()
        {
            Console.WriteLine($"Starting {nameof(DoValueAndReferenceTypes)}.");

            var numbers = Enumerable.Range(0, 127);
            while (!_stopEvent.WaitOne(SleepDurationMs))
            {
                RunValueAndReferenceTypes(numbers);
            }

            Console.WriteLine($"Exiting {nameof(DoValueAndReferenceTypes)}.");
        }

        private void RunBaseTypes(IEnumerable<int> numbers)
        {
            // check the difference when a reference type is part of the generic parameters
            // var container = new GenericContainer<byte, bool, char, int, uint, Int64, UInt64, long, double>();
            var container = new GenericContainer<byte, bool, char, int, string, long, ulong, long, double>();
            for (byte i = 0; i < numbers.Count(); i++)
            {
                container.Store(i, i % 2 == 0);
            }

            for (byte i = 0; i < numbers.Count(); i++)
            {
                container.TryGet(i, out _);
            }

            for (byte i = 0; i < numbers.Count(); i++)
            {
                // check the difference when a reference type is part of generic parameters
                // container.LongGenericParameterList<byte, bool, bool, bool, bool, bool, bool, bool>(i, out _);
                container.LongGenericParameterList<byte, bool, bool, bool, string, bool, bool, bool>(i, out _);
            }
        }

        private void RunValueAndReferenceTypes(IEnumerable<int> numbers)
        {
            var coupleR = new GenericCouple<byte, ReferenceType>();
            var coupleO = new GenericCouple<byte, object>();
            var coupleV = new GenericCouple<byte, ValueType>();

            for (byte i = 0; i < numbers.Count(); i++)
            {
                if (_stopEvent.WaitOne(0))
                {
                    return;
                }

                coupleV.Store(i, new ValueType(i));
                coupleR.Store(i, new ReferenceType(i));
                coupleO.Store(i, new object());
            }

            for (byte i = 0; i < numbers.Count(); i++)
            {
                if (_stopEvent.WaitOne(0))
                {
                    return;
                }

                coupleV.Get(i);
                coupleR.Get(i);
                coupleO.Get(i);
            }
        }
    }

    public class GenericContainer<K, V, T10, T20, T30, T40, T50, T60, T70>
    {
        private Dictionary<K, V> _storage = new Dictionary<K, V>();

        public void Store(K key, V val)
        {
            _storage.Add(key, val);
        }

        public bool TryGet(K key, out V val)
        {
            return _storage.TryGetValue(key, out val);
        }

        public bool LongGenericParameterList<MT1, MT2, MT3, MT4, MT5, MT6, MT7, MT8>(K key, out V val)
        {
            return TryGet(key, out val);
        }
    }

    public class GenericCouple<K, V>
    {
        public void Store(K key, V val)
        {
            Thread.Sleep(0);
        }

        public V Get(K key)
        {
            Thread.Sleep(0);
            return default(V);
        }
    }

    public class ReferenceType
    {
        private int _value;

        public ReferenceType(int val)
        {
            _value = val;
        }

        public int Get() => _value;
    }
}
#pragma warning restore SA1314
#pragma warning restore SA1402
#pragma warning restore SA1649