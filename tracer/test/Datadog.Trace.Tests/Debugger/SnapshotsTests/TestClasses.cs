using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS0414
#pragma warning disable SA1401

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    internal class InfiniteRecursion
    {
        private int number = 666;
        private InfiniteRecursion soInfinite;

        public InfiniteRecursion()
        {
            soInfinite = this;
            Console.Write(number);
        }
    }

    internal class ComplexTestObject
    {
        private string stringField = "test";
        private int intField = 42;
        private List<string> listField = new() { "a", "b", "c" };
        private Dictionary<string, int> dictField = new() { { "key", 1 } };
        private NestedObject nestedField = new() { Value = "nested" };
        private string[] arrayField = { "x", "y", "z" };
    }

    internal class ObjectWithNulls
    {
        private string nullString = null;
        private object nullObject = null;
        private List<string> nullList = null;
        private string[] nullArray = null;
        private NestedObject nullNested = null;
    }

    internal class CircularReference
    {
        public CircularReference Self;
        private string value = "circular";
    }

    internal class EmptyCollections
    {
        private List<string> emptyList = new();
        private Dictionary<string, int> emptyDict = new();
        private string[] emptyArray = Array.Empty<string>();
        private HashSet<int> emptySet = new();
    }

    internal class ComplexKey
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public override bool Equals(object obj) => obj is ComplexKey key && Id == key.Id && Name == key.Name;

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Id.GetHashCode();
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    internal class ObjectWithProperties
    {
        private string backingField = "backing";

        public static string StaticProperty { get; set; } = "static";

        public string AutoProperty { get; set; } = "auto";

        public string ManualProperty
        {
            get => backingField;
            set => backingField = value;
        }

        public string ReadOnlyProperty => "readonly";
    }

    internal class ObjectWithStaticFields
    {
        private static readonly string StaticReadonlyField = "static-readonly";
        private static string staticField = "static";
        private string instanceField = "instance";
    }

    internal class GenericTestObject<T1, T2>
    {
        public T1 Value1;
        public T2 Value2;
        public List<T1> GenericList;
        public Dictionary<T1, T2> GenericDict;
    }

    internal class ObjectWithThrowingProperties
    {
        private string anotherGoodProperty = "another-good";

        public string GoodProperty => "good";

        public string ThrowingProperty => throw new InvalidOperationException("Property access failed");
    }

    internal class ConcurrentCollections
    {
        private ConcurrentDictionary<string, int> concurrentDict = new();
        private ConcurrentQueue<string> concurrentQueue = new();
        private ConcurrentStack<int> concurrentStack = new();
        private ConcurrentBag<string> concurrentBag = new();

        public ConcurrentCollections()
        {
            concurrentDict["key1"] = 1;
            concurrentDict["key2"] = 2;
            concurrentQueue.Enqueue("item1");
            concurrentQueue.Enqueue("item2");
            concurrentStack.Push(1);
            concurrentStack.Push(2);
            concurrentBag.Add("bag1");
            concurrentBag.Add("bag2");
        }
    }

    internal class LazyObjects
    {
        private Lazy<string> uninitializedLazy = new(() => "lazy-value");
        private Lazy<int> initializedLazy;
        private Lazy<ComplexTestObject> lazyComplex = new(() => new ComplexTestObject());

        public LazyObjects()
        {
            initializedLazy = new Lazy<int>(() => 42);
            _ = initializedLazy.Value; // Force initialization
        }
    }

    internal class TaskObjects
    {
        private Task<string> completedTask;
        private Task<int> faultedTask;
        private Task runningTask;

        public TaskObjects()
        {
            completedTask = Task.FromResult("completed");
            faultedTask = Task.FromException<int>(new InvalidOperationException("task failed"));
            runningTask = Task.Delay(TimeSpan.FromHours(1)); // Long running task
        }
    }

    internal class ObjectWithAutoProperties
    {
        public string AutoProp1 { get; set; } = "auto1";

        public int AutoProp2 { get; set; } = 123;

        public List<string> AutoProp3 { get; set; } = new() { "list-item" };
    }

    internal class NestedObject
    {
        public string Value;
        public NestedObject Child;
    }

    internal class SlowSerializationObject
    {
        public SlowSerializationObject()
        {
            // Create a structure that would be slow to serialize
            SlowList = new List<ComplexTestObject>();
            for (var i = 0; i < 1000; i++)
            {
                SlowList.Add(new ComplexTestObject());
            }

            SlowDict = new Dictionary<string, ComplexTestObject>();
            for (var i = 0; i < 1000; i++)
            {
                SlowDict[$"key{i}"] = new ComplexTestObject();
            }

            // Create deep nesting that might trigger depth limits
            DeepNesting = SnapshotHelper.CreateDeeplyNestedObject(50);
        }

        // Object designed to potentially trigger timeout during serialization
        public List<ComplexTestObject> SlowList { get; set; }

        public Dictionary<string, ComplexTestObject> SlowDict { get; set; }

        public NestedObject DeepNesting { get; set; }
    }

    internal class ObjectWithRedactedFields
    {
        // Fields that might trigger redaction based on naming patterns
        private string password = "secret123";
        private string apiKey = "api_key_12345";
        private string token = "bearer_token_xyz";
        private string secret = "top_secret";
        private string key = "encryption_key";
        private string credential = "user_credential";

        // Normal fields that shouldn't be redacted
        private string normalField = "normal_value";
        private int numericField = 42;
    }

    internal class MultipleIssuesObject
    {
        // This object is designed to trigger multiple NotCapturedReason conditions

        // Many fields to trigger fieldCount limit
        private string field1 = "value1";
        private string field2 = "value2";
        private string field3 = "value3";
        private string field4 = "value4";
        private string field5 = "value5";
        private string field6 = "value6";
        private string field7 = "value7";
        private string field8 = "value8";
        private string field9 = "value9";
        private string field10 = "value10";
        private string field11 = "value11";
        private string field12 = "value12";
        private string field13 = "value13";
        private string field14 = "value14";
        private string field15 = "value15";
        private string field16 = "value16";
        private string field17 = "value17";
        private string field18 = "value18";
        private string field19 = "value19";
        private string field20 = "value20";
        private string field21 = "value21"; // Exceeds typical field limit
        private string field22 = "value22";
        private string field23 = "value23";
        private string field24 = "value24";
        private string field25 = "value25";

        // Potentially redacted fields
        private string password = "secret";
        private string apiKey = "key123";

        public MultipleIssuesObject()
        {
            LargeCollection = Enumerable.Range(1, 200).Select(i => $"item{i}").ToList();
            DeepObject = SnapshotHelper.CreateDeeplyNestedObject(15);
        }

        // Large collection to trigger collectionSize limit
        public List<string> LargeCollection { get; set; }

        // Deep nesting to trigger depth limit
        public NestedObject DeepObject { get; set; }

        // Throwing property
        public string ThrowingProperty => throw new InvalidOperationException("Intentional exception");
    }

    internal class ConcurrentModificationCollection
    {
        public ConcurrentModificationCollection()
        {
            ModifiableList = new List<string> { "item1", "item2", "item3" };
            ModifiableDict = new Dictionary<string, int> { { "key1", 1 }, { "key2", 2 } };

            // Simulate concurrent modification during serialization
            // Note: This is a simplified simulation - real concurrent modification
            // would require actual threading during serialization
            Task.Run(async () =>
            {
                await Task.Delay(1); // Small delay
                try
                {
                    ModifiableList.Add("concurrent_item");
                    ModifiableDict["concurrent_key"] = 999;
                }
                catch
                {
                    // Ignore exceptions from concurrent modification
                }
            });
        }

        public List<string> ModifiableList { get; set; }

        public Dictionary<string, int> ModifiableDict { get; set; }
    }
}
