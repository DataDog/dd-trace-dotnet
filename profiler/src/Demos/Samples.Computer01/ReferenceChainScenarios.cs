// <copyright file="ReferenceChainScenarios.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Reference chain test scenarios for heap snapshot testing.
    /// Each scenario creates specific object graph patterns to validate
    /// the type-level reference chain traversal and JSON serialization.
    /// </summary>
    public class ReferenceChainScenarios : ScenarioBase
    {
        private readonly int _scenarioNumber;
        private readonly List<object> _roots = new List<object>();

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

            _roots.Add(orders);
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

            _roots.Add(ordersByCustomer);
            _roots.Add(allProducts);
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

            _roots.Add(rootNodes);
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

            _roots.Add(chains);
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

            _roots.Add(branches);
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

            _roots.Add(containers);
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

            _roots.Add(orderMap);

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

            _roots.Add(forest);

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

            _roots.Add(deepChains);
        }
    }
}
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
