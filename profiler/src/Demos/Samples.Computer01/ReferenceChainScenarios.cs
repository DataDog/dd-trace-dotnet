// <copyright file="ReferenceChainScenarios.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
namespace Samples.Computer01
{
    // Simple chain types
    public class Order
    {
        public Customer Customer { get; set; }
        public string Description { get; set; }
        public List<OrderItem> Items { get; set; }
    }

    public class Customer
    {
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class OrderItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
    }

    public class Product
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    // Cyclic types
    public class TreeNode
    {
        public TreeNode(string data)
        {
            Data = data;
            Children = new List<TreeNode>();
        }

        public TreeNode Parent { get; set; }
        public List<TreeNode> Children { get; set; }
        public string Data { get; set; }

        public TreeNode AddChild(string data)
        {
            var child = new TreeNode(data) { Parent = this };
            Children.Add(child);
            return child;
        }
    }

    // Deep hierarchy types
#pragma warning disable SA1502 // Element should not be on a single line
    public class Level0 { public Level1 Next { get; set; } public string Data { get; set; } }
    public class Level1 { public Level2 Next { get; set; } public string Data { get; set; } }
    public class Level2 { public Level3 Next { get; set; } public string Data { get; set; } }
    public class Level3 { public Level4 Next { get; set; } public string Data { get; set; } }
    public class Level4 { public Level5 Next { get; set; } public string Data { get; set; } }
    public class Level5 { public Level6 Next { get; set; } public string Data { get; set; } }
    public class Level6 { public Level7 Next { get; set; } public string Data { get; set; } }
    public class Level7 { public Level8 Next { get; set; } public string Data { get; set; } }
    public class Level8 { public Level9 Next { get; set; } public string Data { get; set; } }
    public class Level9 { public string Data { get; set; } }
#pragma warning restore SA1502 // Element should not be on a single line

    // Wide tree types
    public class WideBranch
    {
        public WideBranch(string name, int leafCount)
        {
            Name = name;
            Leaves = new List<Leaf>(leafCount);
            for (int i = 0; i < leafCount; i++)
            {
                Leaves.Add(new Leaf { Value = $"{name}-leaf-{i}" });
            }
        }

        public string Name { get; set; }
        public List<Leaf> Leaves { get; set; }
    }

    public class Leaf
    {
        public string Value { get; set; }
    }

    // Linked list type (self-referencing chain without cycles)
    public class LinkedNode
    {
        public LinkedNode Next { get; set; }
        public string Value { get; set; }
    }

    // Shared references types
    public class SharedHolder
    {
        public SharedPayload Shared { get; set; }
        public string Label { get; set; }
    }

    public class SharedPayload
    {
        public byte[] Data { get; set; }
        public string Tag { get; set; }
    }

    // Null fields type
    public class SparseObject
    {
        public Customer FilledRef { get; set; }
        public Product NullRef1 { get; set; }
        public Address NullRef2 { get; set; }
        public Order NullRef3 { get; set; }
        public string Name { get; set; }
    }

    // Value type (struct) with reference fields — used to test traversal of
    // value type arrays such as Dictionary<K,V>.Entry[] whose elements contain
    // references to heap-allocated objects.
#pragma warning disable SA1502 // Element should not be on a single line
    public struct StructWithReferences
    {
        public Customer Customer { get; set; }
        public Product Product { get; set; }
        public int Id { get; set; }
    }
#pragma warning restore SA1502 // Element should not be on a single line

    // Mixed structure types
    public class Container
    {
        public Dictionary<string, Payload> Payloads { get; set; }
        public object[][] Matrix { get; set; }
    }

    public class Payload
    {
        public byte[] Data { get; set; }
        public Metadata Meta { get; set; }
    }

    public class Metadata
    {
        public string Key { get; set; }
        public string[] Tags { get; set; }
    }

    // Scenario 13: Event handler leak
    public class EventPublisher
    {
        public event EventHandler<EventArgs> DataReceived;

        public void Raise()
        {
            DataReceived?.Invoke(this, EventArgs.Empty);
        }
    }

    public class EventSubscriber
    {
        public LeakedPayload Payload { get; }

        public EventSubscriber(LeakedPayload payload)
        {
            Payload = payload;
        }

        public void OnDataReceived(object sender, EventArgs e)
        {
        }
    }

    public class LeakedPayload
    {
        public byte[] Data { get; } = new byte[1024];
    }

    // Scenario 14: Closure / captured variable leak
    public class ClosureHolder
    {
        public List<Func<string>> Callbacks { get; } = new List<Func<string>>();
    }

    public class ExpensiveResource
    {
        public byte[] Buffer { get; } = new byte[4096];

        public override string ToString() => "resource";
    }

    // Scenario 15: Timer / callback leak
    public class TimerOwner
    {
        private readonly Timer _timer;

        public TimerOwner(MonitoredService service)
        {
            Service = service;
            _timer = new Timer(_ => Service.Check(), null, 0, Timeout.Infinite);
        }

        public MonitoredService Service { get; }
    }

    public class MonitoredService
    {
        public ServiceMetrics Metrics { get; } = new ServiceMetrics();

        public void Check()
        {
        }
    }

    public class ServiceMetrics
    {
        public long[] Samples { get; } = new long[256];
    }

    // Scenario 16: Strong GCHandle leak
    public class HandleTarget
    {
        public InteropPayload Payload { get; } = new InteropPayload();
    }

    public class InteropPayload
    {
        public byte[] NativeBuffer { get; } = new byte[2048];
    }

    // Scenario 19: Async state machine leak
    public class AsyncLeakSource
    {
        public List<Task> PendingTasks { get; } = new List<Task>();

        public void StartLeakingOperation(HeavyContext context)
        {
            var tcs = new TaskCompletionSource<bool>();
            PendingTasks.Add(ProcessAsync(tcs.Task, context));
        }

        private async Task ProcessAsync(Task<bool> gate, HeavyContext ctx)
        {
            await gate;
            ctx.Process();
        }
    }

    public class HeavyContext
    {
        public byte[] Buffer { get; } = new byte[8192];

        public void Process()
        {
        }
    }

    // Scenario 20: Nested inline value types containing references.
    // OuterHolder<InnerStruct> embeds InnerStruct as an inline field (ELEMENT_TYPE_VAR
    // resolving to a value type). InnerStruct itself contains a reference field (Target)
    // AND a nested value type (NestedInner) that also contains a reference field (DeepTarget).
    // This exercises the recursive EnqueueInlineValueTypeReferences path.
    public class NestedVtTarget
    {
        public byte[] Data { get; } = new byte[512];
    }

    public class DeepVtTarget
    {
        public byte[] Blob { get; } = new byte[256];
    }

    public struct NestedInnerStruct
    {
        public DeepVtTarget DeepRef { get; set; }
        public int Padding { get; set; }
    }

    public struct InnerStruct
    {
        public NestedVtTarget ShallowRef { get; set; }
        public NestedInnerStruct Nested { get; set; }
        public int Value { get; set; }
    }

    public class OuterHolder<T>
    {
        public T Inline { get; set; }
        public string Label { get; set; }
    }

    /// <summary>
    /// Reference chain test scenarios for heap snapshot testing.
    /// Each scenario creates specific object graph patterns to validate
    /// the type-level reference chain traversal and JSON serialization.
    /// </summary>
    public class ReferenceChainScenarios : ScenarioBase
    {
        private readonly int _scenarioNumber;

        private Dictionary<int, Order> _orderMap;
        private Dictionary<string, List<Order>> _ordersByCustomer;
        private List<Product> _allProducts;
        private List<TreeNode> _treeNodes;
        private List<Level0> _deepChains;
        private List<WideBranch> _branches;
        private List<Container> _containers;
        private List<SharedHolder> _holders;
        private List<SharedPayload> _sharedPayloads;
        private List<LinkedNode> _linkedChains;
        private List<SparseObject> _sparseObjects;
        private StructWithReferences[] _structEntries;

        private static List<Order> _staticOrders;

        // Scenarios 13-19: Memory leak patterns
        private EventPublisher _eventPublisher;
        private ClosureHolder _closureHolder;
        private List<TimerOwner> _timerOwners;
        private List<GCHandle> _gcHandles;
        private List<GCHandle> _pinnedHandles;
        private AsyncLeakSource _asyncLeakSource;
        private List<OuterHolder<InnerStruct>> _nestedVtHolders;

        public ReferenceChainScenarios(int scenarioNumber)
        {
            _scenarioNumber = scenarioNumber;
        }

        public override void OnProcess()
        {
            switch (_scenarioNumber)
            {
                case 1:
                    RunSimpleChain();
                    break;
                case 2:
                    RunMultipleRoots();
                    break;
                case 3:
                    RunCycles();
                    break;
                case 4:
                    RunDeepHierarchy();
                    break;
                case 5:
                    RunWideTree();
                    break;
                case 6:
                    RunMixedStructures();
                    break;
                case 7:
                    RunLargeScale();
                    break;
                case 8:
                    RunSharedReferences();
                    break;
                case 9:
                    RunLinkedList();
                    break;
                case 10:
                    RunNullFields();
                    break;
                case 11:
                    RunStructWithReferences();
                    break;
                case 12:
                    RunStaticRootSimpleChain();
                    break;
                case 13:
                    RunEventHandlerLeak();
                    break;
                case 14:
                    RunClosureLeak();
                    break;
                case 15:
                    RunTimerLeak();
                    break;
                case 16:
                    RunGCHandleLeak();
                    break;
                case 18:
                    RunPinnedLeak();
                    break;
                case 19:
                    RunAsyncLeak();
                    break;
                case 20:
                    RunNestedValueType();
                    break;
                default:
                    RunSimpleChain();
                    break;
            }

            // Keep objects alive and wait for profiler to capture heap snapshot
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            // Give time for heap snapshot to be captured
            WaitFor(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Scenario 1: Simple Chain (~1K objects)
        /// Static Dictionary → Order → Customer → Address
        ///                           → OrderItem[] → Product
        /// </summary>
        private void RunSimpleChain()
        {
            Console.WriteLine("ReferenceChain Scenario 1: Simple Chain");
            var orders = new Dictionary<int, Order>();

            for (int i = 0; i < 100; i++)
            {
                var order = new Order
                {
                    Description = $"Order-{i}",
                    Customer = new Customer
                    {
                        Name = $"Customer-{i}",
                        Address = new Address
                        {
                            Street = $"Street-{i}",
                            City = $"City-{i}"
                        }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            Product = new Product { Name = $"Product-{i}-A", Price = i * 10 },
                            Quantity = i + 1
                        },
                        new OrderItem
                        {
                            Product = new Product { Name = $"Product-{i}-B", Price = i * 20 },
                            Quantity = i + 2
                        }
                    }
                };
                orders[i] = order;
            }

            _orderMap = orders;
        }

        /// <summary>
        /// Scenario 2: Multiple Roots (~5K objects)
        /// Root 1: Static Dictionary → Order → Customer
        /// Root 2: Static List → Product
        /// </summary>
        private void RunMultipleRoots()
        {
            Console.WriteLine("ReferenceChain Scenario 2: Multiple Roots");
            var ordersByCustomer = new Dictionary<string, List<Order>>();
            var allProducts = new List<Product>();

            for (int i = 0; i < 500; i++)
            {
                var customer = new Customer
                {
                    Name = $"Customer-{i}",
                    Address = new Address { Street = $"Street-{i}", City = $"City-{i}" }
                };

                var orders = new List<Order>();
                for (int j = 0; j < 3; j++)
                {
                    var product = new Product { Name = $"Product-{i}-{j}", Price = i * j };
                    allProducts.Add(product);

                    orders.Add(new Order
                    {
                        Description = $"Order-{i}-{j}",
                        Customer = customer,
                        Items = new List<OrderItem>
                        {
                            new OrderItem { Product = product, Quantity = j + 1 }
                        }
                    });
                }

                ordersByCustomer[$"Customer-{i}"] = orders;
            }

            _ordersByCustomer = ordersByCustomer;
            _allProducts = allProducts;
        }

        /// <summary>
        /// Scenario 3: Cycles (~50K objects)
        /// Parent → Child → Parent (bidirectional tree)
        /// </summary>
        private void RunCycles()
        {
            Console.WriteLine("ReferenceChain Scenario 3: Cycles");
            var rootNodes = new List<TreeNode>();

            for (int i = 0; i < 100; i++)
            {
                var root = new TreeNode($"Root-{i}");
                for (int j = 0; j < 50; j++)
                {
                    var child = root.AddChild($"Child-{i}-{j}");
                    for (int k = 0; k < 10; k++)
                    {
                        child.AddChild($"GrandChild-{i}-{j}-{k}");
                    }
                }

                rootNodes.Add(root);
            }

            _treeNodes = rootNodes;
        }

        /// <summary>
        /// Scenario 4: Deep Hierarchy (~100K objects)
        /// Root → Level0 → Level1 → ... → Level9
        /// </summary>
        private void RunDeepHierarchy()
        {
            Console.WriteLine("ReferenceChain Scenario 4: Deep Hierarchy");
            var chains = new List<Level0>();

            for (int i = 0; i < 10000; i++)
            {
                chains.Add(new Level0
                {
                    Data = $"L0-{i}",
                    Next = new Level1
                    {
                        Data = $"L1-{i}",
                        Next = new Level2
                        {
                            Data = $"L2-{i}",
                            Next = new Level3
                            {
                                Data = $"L3-{i}",
                                Next = new Level4
                                {
                                    Data = $"L4-{i}",
                                    Next = new Level5
                                    {
                                        Data = $"L5-{i}",
                                        Next = new Level6
                                        {
                                            Data = $"L6-{i}",
                                            Next = new Level7
                                            {
                                                Data = $"L7-{i}",
                                                Next = new Level8
                                                {
                                                    Data = $"L8-{i}",
                                                    Next = new Level9
                                                    {
                                                        Data = $"L9-{i}"
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            _deepChains = chains;
        }

        /// <summary>
        /// Scenario 5: Wide Tree (~200K objects)
        /// Root → 100 branches → each with 50 leaves
        /// </summary>
        private void RunWideTree()
        {
            Console.WriteLine("ReferenceChain Scenario 5: Wide Tree");
            var branches = new List<WideBranch>();

            for (int i = 0; i < 100; i++)
            {
                branches.Add(new WideBranch($"Branch-{i}", 50));
            }

            _branches = branches;
        }

        /// <summary>
        /// Scenario 6: Mixed Structures (~1M objects)
        /// Arrays of arrays, Dictionaries with complex values
        /// </summary>
        private void RunMixedStructures()
        {
            Console.WriteLine("ReferenceChain Scenario 6: Mixed Structures");
            var containers = new List<Container>();

            for (int i = 0; i < 1000; i++)
            {
                var container = new Container
                {
                    Payloads = new Dictionary<string, Payload>(),
                    Matrix = new object[10][]
                };

                for (int j = 0; j < 10; j++)
                {
                    container.Payloads[$"key-{i}-{j}"] = new Payload
                    {
                        Data = new byte[100],
                        Meta = new Metadata
                        {
                            Key = $"meta-{i}-{j}",
                            Tags = new[] { $"tag1-{i}", $"tag2-{j}" }
                        }
                    };

                    container.Matrix[j] = new object[10];
                    for (int k = 0; k < 10; k++)
                    {
                        container.Matrix[j][k] = new Leaf { Value = $"matrix-{i}-{j}-{k}" };
                    }
                }

                containers.Add(container);
            }

            _containers = containers;
        }

        /// <summary>
        /// Scenario 7: Large Scale (~10M objects)
        /// Multiple roots, deep and wide, with cycles
        /// </summary>
        private void RunLargeScale()
        {
            Console.WriteLine("ReferenceChain Scenario 7: Large Scale");

            // Root 1: Large dictionary of orders
            var orderMap = new Dictionary<int, Order>();
            for (int i = 0; i < 50000; i++)
            {
                orderMap[i] = new Order
                {
                    Description = $"Order-{i}",
                    Customer = new Customer
                    {
                        Name = $"Cust-{i}",
                        Address = new Address { Street = $"St-{i}", City = $"C-{i}" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Product = new Product { Name = $"P-{i}", Price = i }, Quantity = 1 }
                    }
                };
            }

            _orderMap = orderMap;

            // Root 2: Tree with cycles
            var forest = new List<TreeNode>();
            for (int i = 0; i < 1000; i++)
            {
                var tree = new TreeNode($"Tree-{i}");
                for (int j = 0; j < 20; j++)
                {
                    var branch = tree.AddChild($"Branch-{i}-{j}");
                    for (int k = 0; k < 10; k++)
                    {
                        branch.AddChild($"Leaf-{i}-{j}-{k}");
                    }
                }

                forest.Add(tree);
            }

            _treeNodes = forest;

            // Root 3: Deep chains
            var deepChains = new List<Level0>();
            for (int i = 0; i < 5000; i++)
            {
                deepChains.Add(new Level0
                {
                    Data = $"D0-{i}",
                    Next = new Level1
                    {
                        Data = $"D1-{i}",
                        Next = new Level2
                        {
                            Data = $"D2-{i}",
                            Next = new Level3
                            {
                                Data = $"D3-{i}",
                                Next = new Level4
                                {
                                    Data = $"D4-{i}",
                                    Next = new Level5
                                    {
                                        Data = $"D5-{i}",
                                        Next = new Level6
                                        {
                                            Data = $"D6-{i}",
                                            Next = new Level7
                                            {
                                                Data = $"D7-{i}",
                                                Next = new Level8
                                                {
                                                    Data = $"D8-{i}",
                                                    Next = new Level9
                                                    {
                                                        Data = $"D9-{i}"
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            _deepChains = deepChains;
        }

        /// <summary>
        /// Scenario 8: Shared References (~500 objects)
        /// Multiple parent objects reference the same shared child.
        /// Tests that shared objects are correctly traversed from each root
        /// (VisitedObjectSet is per-root, so the same object appears in multiple paths).
        /// </summary>
        private void RunSharedReferences()
        {
            Console.WriteLine("ReferenceChain Scenario 8: Shared References");
            var holders = new List<SharedHolder>();

            // Create shared payloads that will be referenced by multiple holders
            var sharedPayloads = new List<SharedPayload>();
            for (int i = 0; i < 10; i++)
            {
                sharedPayloads.Add(new SharedPayload
                {
                    Data = new byte[256],
                    Tag = $"shared-payload-{i}"
                });
            }

            // Each holder references one of the shared payloads (many-to-one)
            for (int i = 0; i < 100; i++)
            {
                holders.Add(new SharedHolder
                {
                    Shared = sharedPayloads[i % sharedPayloads.Count],
                    Label = $"holder-{i}"
                });
            }

            _holders = holders;
            _sharedPayloads = sharedPayloads;
        }

        /// <summary>
        /// Scenario 9: Linked List / Self-Referencing Chain (~1K objects)
        /// LinkedNode -> LinkedNode -> LinkedNode -> ... -> null
        /// Same type at every level: tests the type tree's ability to represent
        /// TypeA -> TypeA -> TypeA as distinct tree nodes at each depth.
        /// </summary>
        private void RunLinkedList()
        {
            Console.WriteLine("ReferenceChain Scenario 9: Linked List");
            var chains = new List<LinkedNode>();

            for (int i = 0; i < 50; i++)
            {
                // Build a chain of 20 nodes
                LinkedNode head = null;
                for (int j = 19; j >= 0; j--)
                {
                    head = new LinkedNode
                    {
                        Value = $"node-{i}-{j}",
                        Next = head
                    };
                }

                chains.Add(head);
            }

            _linkedChains = chains;
        }

        /// <summary>
        /// Scenario 10: Null Fields (~500 objects)
        /// Objects with many reference fields intentionally left null.
        /// Tests that null references are correctly skipped without errors.
        /// Only the non-null field (FilledRef) should produce children in the tree.
        /// </summary>
        private void RunNullFields()
        {
            Console.WriteLine("ReferenceChain Scenario 10: Null Fields");
            var sparseObjects = new List<SparseObject>();

            for (int i = 0; i < 100; i++)
            {
                sparseObjects.Add(new SparseObject
                {
                    // Only FilledRef is set; NullRef1, NullRef2, NullRef3 are all null
                    FilledRef = new Customer
                    {
                        Name = $"customer-{i}",
                        Address = (i % 3 == 0) ? new Address { Street = $"street-{i}", City = $"city-{i}" } : null
                    },
                    NullRef1 = null,
                    NullRef2 = null,
                    NullRef3 = null,
                    Name = $"sparse-{i}"
                });
            }

            _sparseObjects = sparseObjects;
        }

        /// <summary>
        /// Scenario 11: Struct with References (~300 objects)
        /// An array of value types (structs) whose fields hold references to
        /// heap-allocated objects. This exercises traversal of value type arrays
        /// where the struct layout contains reference fields — the same pattern
        /// used by Dictionary&lt;K,V&gt;.Entry[].
        /// StructWithReferences[] → Customer → Address
        ///                        → Product
        /// </summary>
        private void RunStructWithReferences()
        {
            Console.WriteLine("ReferenceChain Scenario 11: Struct with References");
            var entries = new StructWithReferences[100];

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new StructWithReferences
                {
                    Customer = new Customer
                    {
                        Name = $"Customer-{i}",
                        Address = new Address { Street = $"Street-{i}", City = $"City-{i}" }
                    },
                    Product = new Product { Name = $"Product-{i}", Price = i * 10 },
                    Id = i
                };
            }

            _structEntries = entries;
        }

        /// <summary>
        /// Scenario 12: Static Root Simple Chain
        /// Same object graph as Scenario 1 but held by a static field so the GC
        /// reports it through GCBulkRootStaticVar instead of a stack/handle root.
        /// Static List&lt;Order&gt; → Order → Customer → Address
        ///                                → OrderItem[] → Product
        /// </summary>
        private void RunStaticRootSimpleChain()
        {
            Console.WriteLine("ReferenceChain Scenario 12: Static Root Simple Chain");
            var orders = new List<Order>();

            for (int i = 0; i < 100; i++)
            {
                orders.Add(new Order
                {
                    Description = $"Order-{i}",
                    Customer = new Customer
                    {
                        Name = $"Customer-{i}",
                        Address = new Address
                        {
                            Street = $"Street-{i}",
                            City = $"City-{i}"
                        }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            Product = new Product { Name = $"Product-{i}-A", Price = i * 10 },
                            Quantity = i + 1
                        },
                        new OrderItem
                        {
                            Product = new Product { Name = $"Product-{i}-B", Price = i * 20 },
                            Quantity = i + 2
                        }
                    }
                });
            }

            _staticOrders = orders;
        }

        /// <summary>
        /// Scenario 13: Event Handler Leak
        /// Publisher holds subscribers alive via event delegate invocation list.
        /// EventPublisher -> (delegate) -> EventSubscriber -> LeakedPayload
        /// </summary>
        private void RunEventHandlerLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 13: Event Handler Leak");
            var publisher = new EventPublisher();
            var payload = new LeakedPayload();
            var subscriber = new EventSubscriber(payload);
            publisher.DataReceived += subscriber.OnDataReceived;

            _eventPublisher = publisher;
        }

        /// <summary>
        /// Scenario 14: Closure / Captured Variable Leak
        /// Lambdas capturing expensive objects via compiler-generated display classes.
        /// ClosureHolder -> List of Func -> DisplayClass -> ExpensiveResource
        /// </summary>
        private void RunClosureLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 14: Closure Leak");
            var holder = new ClosureHolder();
            for (int i = 0; i < 50; i++)
            {
                var resource = new ExpensiveResource();
                holder.Callbacks.Add(() => resource.ToString());
            }

            _closureHolder = holder;
        }

        /// <summary>
        /// Scenario 15: Timer / Callback Leak
        /// System.Threading.Timer keeping callback targets alive.
        /// Timer -> TimerCallback -> TimerOwner -> MonitoredService -> ServiceMetrics
        /// </summary>
        private void RunTimerLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 15: Timer Leak");
            var owners = new List<TimerOwner>();
            for (int i = 0; i < 20; i++)
            {
                var service = new MonitoredService();
                owners.Add(new TimerOwner(service));
            }

            _timerOwners = owners;
        }

        /// <summary>
        /// Scenario 16: Strong GCHandle Leak (Handle root kind)
        /// GCHandle.Alloc without Free() - tests Handle root category.
        /// [Handle root] -> HandleTarget -> InteropPayload
        /// </summary>
        private void RunGCHandleLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 16: GCHandle Leak");
            var handles = new List<GCHandle>();
            for (int i = 0; i < 50; i++)
            {
                var target = new HandleTarget();
                handles.Add(GCHandle.Alloc(target, GCHandleType.Normal));
            }

            _gcHandles = handles;
        }

        /// <summary>
        /// Scenario 18: Pinned Handle (Pinning root kind)
        /// GCHandle.Alloc with Pinned - tests Pinning root category.
        /// </summary>
        private void RunPinnedLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 18: Pinned Leak");
            var handles = new List<GCHandle>();
            for (int i = 0; i < 20; i++)
            {
                var buffer = new byte[4096];
                buffer[0] = 1; // Prevent optimization - ensure buffer is considered used
                handles.Add(GCHandle.Alloc(buffer, GCHandleType.Pinned));
            }

            _pinnedHandles = handles;
        }

        /// <summary>
        /// Scenario 19: Async State Machine Leak
        /// Async methods awaiting never-completing Task keep state machine and captured locals alive.
        /// AsyncLeakSource -> List of Task -> Task -> HeavyContext
        /// </summary>
        private void RunAsyncLeak()
        {
            Console.WriteLine("ReferenceChain Scenario 19: Async Leak");
            var source = new AsyncLeakSource();
            for (int i = 0; i < 10; i++)
            {
                var context = new HeavyContext();
                source.StartLeakingOperation(context);
            }

            _asyncLeakSource = source;
        }

        /// <summary>
        /// Scenario 20: Nested Inline Value Types (~100 objects)
        /// OuterHolder&lt;InnerStruct&gt; embeds InnerStruct as a generic type parameter
        /// that resolves to a value type. InnerStruct contains a reference (NestedVtTarget)
        /// and a nested struct (NestedInnerStruct) that itself contains a reference (DeepVtTarget).
        /// Tests the recursive EnqueueInlineValueTypeReferences traversal path.
        /// </summary>
        private void RunNestedValueType()
        {
            Console.WriteLine("ReferenceChain Scenario 20: Nested Inline Value Types");
            var holders = new List<OuterHolder<InnerStruct>>();

            for (int i = 0; i < 50; i++)
            {
                holders.Add(new OuterHolder<InnerStruct>
                {
                    Inline = new InnerStruct
                    {
                        ShallowRef = new NestedVtTarget(),
                        Nested = new NestedInnerStruct
                        {
                            DeepRef = new DeepVtTarget(),
                            Padding = i
                        },
                        Value = i
                    },
                    Label = $"holder-{i}"
                });
            }

            _nestedVtHolders = holders;
        }
    }
}
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
