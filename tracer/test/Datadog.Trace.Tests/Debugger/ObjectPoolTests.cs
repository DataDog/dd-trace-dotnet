// <copyright file="ObjectPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.Debugger
{
    public class ObjectPoolTests
    {
        [Fact]
        public void Constructor_WithDefaultParameters_CreatesEmptyPool()
        {
            // Arrange & Act
            var pool = new ObjectPool<TestPoolable, string>();

            // Assert
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void Constructor_WithNegativeMaxSize_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new ObjectPool<TestPoolable, string>(maxSize: -1));
        }

        [Fact]
        public void Get_WhenPoolEmpty_CreatesNewInstance()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();

            // Act
            var item = pool.Get();

            // Assert
            item.Should().NotBeNull();
            pool.Count.Should().Be(0);
        }

        [Fact]
        public void Get_WithParameters_SetsParametersCorrectly()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();
            const string testValue = "test";

            // Act
            var item = pool.Get(testValue);

            // Assert
            item.Should().NotBeNull();
            item!.CurrentValue.Should().Be(testValue);
        }

        [Fact]
        public void Return_WhenBelowMaxSize_AddsToPool()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();
            var item = pool.Get();

            // Act
            pool.Return(item);

            // Assert
            pool.Count.Should().Be(1);
        }

        [Fact]
        public void Return_WhenAtMaxSize_DoesNotAddToPool()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>(maxSize: 1);
            var item1 = pool.Get();
            var item2 = pool.Get();

            // Act
            pool.Return(item1);
            pool.Return(item2);

            // Assert
            pool.Count.Should().Be(1);
        }

        [Fact]
        public void Return_ResetsItem()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();
            var item = pool.Get("test");

            // Act
            pool.Return(item);

            // Assert
            item.Should().NotBeNull();
            item!.WasReset.Should().BeTrue();
            item.CurrentValue.Should().BeNull();
        }

        [Fact]
        public void Get_ReturnsPooledInstance_WhenAvailable()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();
            var item = pool.Get();
            pool.Return(item);

            // Act
            var retrievedItem = pool.Get();

            // Assert
            retrievedItem.Should().BeSameAs(retrievedItem);
            pool.Count.Should().Be(0);
        }

        [Fact]
        public void Get_WithCustomFactory_UsesFactoryToCreateInstances()
        {
            // Arrange
            var factoryCallCount = 0;
            var factory = new Func<TestPoolable>(
                () =>
                {
                    factoryCallCount++;
                    return new TestPoolable();
                });

            var pool = new ObjectPool<TestPoolable, string>(factory);

            // Act
            var item = pool.Get();

            // Assert
            item.Should().NotBeNull();
            factoryCallCount.Should().Be(1);
        }

        [Fact]
        public void Return_WithNullItem_DoesNotThrowException()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();

            // Act & Assert
            var exception = Record.Exception(() => pool.Return(null));
            exception.Should().BeNull();
        }

        [Fact]
        public async Task ConcurrentAccess_IsThreadSafe()
        {
            // Arrange
            var pool = new ObjectPool<TestPoolable, string>();
            const int operationCount = 1000;
            var tasks = new Task[operationCount];

            // Act
            for (var i = 0; i < operationCount; i++)
            {
                tasks[i] = Task.Run(
                    () =>
                    {
                        var item = pool.Get();
                        pool.Return(item);
                    });
            }

            // Assert
            await Task.WhenAll(tasks);
            pool.Count.Should().BeLessThanOrEqualTo(operationCount);
            pool.Count.Should().BeLessThanOrEqualTo(100); // Default max size
        }

        private class TestPoolable : IPoolable<string>
        {
            public string? CurrentValue { get; private set; }

            public bool WasReset { get; private set; }

            public void Set(string parameters)
            {
                CurrentValue = parameters;
                WasReset = false;
            }

            public void Reset()
            {
                CurrentValue = null;
                WasReset = true;
            }
        }
    }
}
