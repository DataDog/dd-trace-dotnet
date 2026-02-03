# Profiler Memory Measurement

This document describes memory measurement capabilities for profiler components to help monitor and optimize memory usage.

## Overview

Memory measurement provides visibility into how much memory is consumed by different profiler components. This helps with:
- **Memory monitoring** - Track consumption in production
- **Leak detection** - Identify unusual growth patterns  
- **Optimization** - Find memory hotspots
- **Debugging** - Detailed breakdown for troubleshooting

## Metrics Integration

Memory footprint metrics are automatically emitted via DogStatsD for monitoring in production.

### Registered Metrics

The following metrics are registered in `CorProfilerCallback::InitializeServices()`:

| Metric Name | Component | Description |
|-------------|-----------|-------------|
| `dotnet_memory_footprint_managed_threads` | ManagedThreadList | Thread management memory |
| `dotnet_memory_footprint_frame_store` | FrameStore | Frame and type cache memory |
| `dotnet_memory_footprint_debug_info` | DebugInfoStore | PDB debug information memory |
| `dotnet_memory_footprint_app_domain_store` | AppDomainStore | AppDomain cache memory |
| `dotnet_memory_footprint_application_store` | ApplicationStore | Application metadata memory |
| `dotnet_memory_footprint_runtime_id_store` | RuntimeIdStore | Runtime ID cache memory |
| `dotnet_memory_footprint_threads_cpu_manager` | ThreadsCpuManager | Thread CPU tracking memory |
| `dotnet_memory_footprint_exceptions_provider` | ExceptionsProvider | Exception profiling memory |
| `dotnet_memory_footprint_heap_snapshot_manager` | HeapSnapshotManager | Heap snapshot memory |
| `dotnet_memory_footprint_network_provider` | NetworkProvider | HTTP/network profiling memory |

### Monitoring Memory Growth

Use these metrics to:
- **Set alerts** for unexpected memory growth
- **Track trends** over time
- **Correlate** with application activity
- **Detect leaks** by observing unbounded growth

## Shutdown Logging

At profiler shutdown, detailed memory breakdown is logged for all components.

### Example Output

```
=== Profiler Memory Breakdown at Shutdown ===
ManagedThreadList Memory Breakdown:
  Base object size:           192 bytes
  Vector storage:             400 bytes (capacity=50)
  Thread count:               10
  ManagedThreadInfo objects:  5120 bytes
  CLR ThreadID lookup map:    1200 bytes (50 buckets)
  OS ThreadID lookup map:     1200 bytes (50 buckets)
  Iterators vector:           8 bytes (2 iterators)
  Total memory:               8120 bytes (7.93 KB)

FrameStore Memory Breakdown:
  Base object size:        128 bytes
  Methods cache:           52480 bytes (120 entries, 256 buckets)
  Types cache:             31744 bytes (75 entries, 128 buckets)
  Native frames cache:     2048 bytes (5 entries, 64 buckets)
  Full type names cache:   8192 bytes (18 entries, 64 buckets)
  Total memory:            94592 bytes (92.38 KB)

DebugInfoStore Memory Breakdown:
  Base object size:        64 bytes
  Modules map:             3200 bytes (5 entries, 64 buckets)
  Module infos content:    125000 bytes
  Total memory:            128264 bytes (125.26 KB)

AppDomainStore Memory Breakdown:
  Base object size:        48 bytes
  Map storage:             384 bytes (2 entries, 8 buckets)
  Strings content:         128 bytes
  Total memory:            560 bytes (0.55 KB)

ApplicationStore Memory Breakdown:
  Base object size:        56 bytes
  Map storage:             512 bytes (1 entries, 8 buckets)
  Runtime ID keys:         64 bytes
  ApplicationInfo content: 512 bytes
  Total memory:            1144 bytes (1.12 KB)

RuntimeIdStore Memory Breakdown:
  Base object size:        72 bytes
  Cache map storage:       192 bytes (0 entries, 8 buckets)
  Runtime IDs content:     0 bytes
  Total memory:            264 bytes (0.26 KB)

ThreadsCpuManager Memory Breakdown:
  Base object size:        96 bytes
  Map storage:             1536 bytes (12 entries, 32 buckets)
  ThreadCpuInfo objects:   2048 bytes
  Total memory:            3680 bytes (3.59 KB)

ExceptionsProvider Memory Breakdown:
  Base object size:        256 bytes
  Exception types map:     2048 bytes (15 entries, 64 buckets)
  Exception type strings:  1280 bytes
  GroupSampler (estimate): 3200 bytes
  Total memory:            6784 bytes (6.63 KB)

HeapSnapshotManager Memory Breakdown:
  Base object size:        384 bytes
  Histogram map storage:   8192 bytes (250 entries, 512 buckets)
  ClassHistogramEntry:     45000 bytes
  Total memory:            53576 bytes (52.32 KB)

NetworkProvider Memory Breakdown:
  Base object size:        192 bytes
  Requests map storage:    4096 bytes (8 entries, 64 buckets)
  NetworkRequestInfo:      12288 bytes
  Total memory:            16576 bytes (16.19 KB)

Total measured profiler memory: 313824 bytes (0.30 MB)
==============================================
```

### Use Cases

Shutdown logging helps with:
- **Post-mortem analysis** of memory usage
- **Regression detection** by comparing logs across versions
- **Debugging** memory issues in customer environments
- **Validation** that memory is as expected for the workload


## Adding Memory Measurement to New Components

To add memory measurement to a new profiler component:

### 1. Update the Interface (if applicable)

If your component is accessed via an interface pointer in `CorProfilerCallback`, make sure the interface inherits from `IMemoryFootprintProvider`:

```cpp
// In IMyComponent.h
#include "IMemoryFootprintProvider.h"

class IMyComponent : public IMemoryFootprintProvider
{
public:
    // ... other interface methods ...
};
```

The `IMemoryFootprintProvider` interface defines:
- `virtual size_t GetMemorySize() const = 0;`
- `virtual void LogMemoryBreakdown() const = 0;`

### 2. Add Method Declarations and Helper Struct

In your concrete component class:

```cpp
class MyComponent : public IMyComponent {
public:
    // Memory measurement (IMemoryFootprintProvider)
    size_t GetMemorySize() const override;
    void LogMemoryBreakdown() const override;
    
private:
    struct MemoryStats
    {
        size_t baseSize;
        size_t vectorSize;
        size_t mapSize;
        size_t itemCount;
        // ... other stats ...
        
        size_t GetTotal() const
        {
            return baseSize + vectorSize + mapSize + /* ... */;
        }
    };
    
    MemoryStats ComputeMemoryStats() const;
    mutable std::mutex _mutex;  // Make mutable if needed for const methods
};
```

### 3. Implement ComputeMemoryStats() Helper
```cpp
MyComponent::MemoryStats MyComponent::ComputeMemoryStats() const {
    std::lock_guard<std::mutex> lock(_mutex);
    
    MemoryStats stats{};
    stats.baseSize = sizeof(MyComponent);
    stats.itemCount = _items.size();
    
    // Add container sizes
    stats.vectorSize = _myVector.capacity() * sizeof(MyType);
    stats.mapSize = _myMap.bucket_count() * (sizeof(Key) + sizeof(Value) + sizeof(void*));
    
    // Add nested object sizes
    for (const auto& item : _items) {
        stats.mapSize += item->GetMemorySize();
    }
    
    return stats;
}
```

### 3. Implement GetMemorySize()
```cpp
size_t MyComponent::GetMemorySize() const {
    return ComputeMemoryStats().GetTotal();
}
```

### 4. Implement LogMemoryBreakdown()
```cpp
void MyComponent::LogMemoryBreakdown() const {
    auto stats = ComputeMemoryStats();
    
    Log::Debug("MyComponent Memory Breakdown:");
    Log::Debug("  Base object size: ", stats.baseSize, " bytes");
    Log::Debug("  Vector storage:   ", stats.vectorSize, " bytes");
    Log::Debug("  Map storage:      ", stats.mapSize, " bytes (", stats.itemCount, " items)");
    Log::Debug("  Total memory:     ", stats.GetTotal(), " bytes (", stats.GetTotal() / 1024.0, " KB)");
}
```

**Benefits of this pattern:**
- **DRY principle** - Memory calculation logic in one place
- **Consistency** - Both methods use identical calculations
- **Maintainability** - Add new metrics in one location
- **Performance** - Single lock acquisition per call


### 6. Register Metrics and Add Shutdown Logging

In `CorProfilerCallback::InitializeServices()`, register the memory footprint metric:

```cpp
_metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_memory_footprint_my_component", [this]() {
    return _pMyComponent->GetMemorySize();  // Via interface pointer
});
```

In `CorProfilerCallback::Shutdown()`, add logging before `DisposeInternal()`:

```cpp
if (_pMyComponent != nullptr)
{
    _pMyComponent->LogMemoryBreakdown();
    totalMemory += _pMyComponent->GetMemorySize();
}
```

### 7. Update This Documentation
Add a new section under "Components with Memory Measurement" following the same structure as `ManagedThreadList` or `FrameStore`.


## Components with Memory Measurement

### ManagedThreadList

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `ManagedThreadList` structure |
| Vector storage | Capacity of `_threads` vector |
| Thread objects | All `ManagedThreadInfo` instances |
| CLR lookup | Hash map from CLR ThreadID → ThreadInfo |
| OS lookup | Hash map from OS ThreadID → ThreadInfo |
| Iterators | Vector tracking active iterators |

#### Example Output

```
ManagedThreadList Memory Breakdown:
  Base object size:           192 bytes
  Vector storage (capacity=50): 400 bytes
  Thread count:               10
  ManagedThreadInfo objects:  5120 bytes
  CLR ThreadID lookup map:    1200 bytes (50 buckets)
  OS ThreadID lookup map:     1200 bytes (50 buckets)
  Iterators vector:           8 bytes (2 iterators)
  Total memory:               8120 bytes (7.93 KB)
```

#### Implementation Details

**ManagedThreadInfo::GetMemorySize()**
- Base object size (`sizeof(ManagedThreadInfo)`)
- Dynamic string allocations (thread names, IDs)
- Uses `capacity()` for actual allocated memory

**ManagedThreadList::GetMemorySize()**
- Base object size
- Vector capacity (not just size)
- Sum of all `ManagedThreadInfo` objects
- Hash map bucket estimates
- Iterator vector capacity


### FrameStore

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `FrameStore` structure |
| Methods cache | Map of FunctionID → FrameInfo (method names and metadata) |
| Types cache | Map of ClassID → TypeDesc (type names and metadata) |
| Native frames cache | Map of native module names → frame strings |
| Full type names cache | Map of ClassID → fully qualified type names (for allocations) |

#### Example Output

```
FrameStore Memory Breakdown:
  Base object size:        240 bytes
  Methods cache:           45600 bytes (150 entries, 211 buckets)
  Types cache:             32400 bytes (100 entries, 211 buckets)
  Native frames cache:     8200 bytes (25 entries, 53 buckets)
  Full type names cache:   12800 bytes (80 entries, 97 buckets)
  Total memory:            99240 bytes (96.91 KB)
```

#### Implementation Details

**FrameStore::GetMemorySize()**
- Base object size
- For each cache map:
  - Bucket count * (key size + value size + pointer overhead)
  - String capacities in stored objects
- Locks each cache separately for fine-grained synchronization

**FrameStore::LogMemoryBreakdown()**
- Separates memory by cache type (methods, types, native frames, full type names)
- Shows both entry counts and bucket counts for each map
- Helps identify which caches are growing


### DebugInfoStore

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `DebugInfoStore` structure |
| Modules map | Map of ModuleID → ModuleDebugInfo |
| Module paths | String storage for module file paths |
| Files vectors | Vectors of source file paths per module |
| Symbols vectors | Vectors of SymbolDebugInfo per module |

#### Example Output

```
DebugInfoStore Memory Breakdown:
  Base object size:        64 bytes
  Modules map:             3200 bytes (5 entries, 64 buckets)
  Module infos content:    125000 bytes
  Total memory:            128264 bytes (125.26 KB)
```


### AppDomainStore

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `AppDomainStore` structure |
| Map storage | Hash map of AppDomainID → std::string |
| Strings | Capacity of all AppDomain name strings |

#### Example Output

```
AppDomainStore Memory Breakdown:
  Base object size:        48 bytes
  Map storage:             384 bytes (2 entries, 8 buckets)
  Strings content:         128 bytes
  Total memory:            560 bytes (0.55 KB)
```


### ApplicationStore

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `ApplicationStore` structure |
| Map storage | Hash map of runtime ID (string) → ApplicationInfo |
| Runtime ID keys | String capacities for all runtime ID keys |
| ApplicationInfo | All strings in ApplicationInfo (ServiceName, Environment, Version, RepositoryUrl, CommitSha, ProcessTags) |

#### Example Output

```
ApplicationStore Memory Breakdown:
  Base object size:        56 bytes
  Map storage:             512 bytes (1 entries, 8 buckets)
  Runtime ID keys:         64 bytes
  ApplicationInfo content: 512 bytes
  Total memory:            1144 bytes (1.12 KB)
```


### RuntimeIdStore

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `RuntimeIdStore` structure |
| Cache map | Hash map of AppDomainID → runtime ID string (fallback only) |
| Runtime ID strings | String capacities for all cached runtime IDs |

#### Example Output

```
RuntimeIdStore Memory Breakdown:
  Base object size:        72 bytes
  Cache map storage:       192 bytes (0 entries, 8 buckets)
  Runtime IDs content:     0 bytes
  Total memory:            264 bytes (0.26 KB)
```

### ThreadsCpuManager

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `ThreadsCpuManager` structure |
| Map storage | Hash map from OS ThreadID → ThreadCpuInfo |
| ThreadCpuInfo objects | All thread CPU info instances including thread names |

#### Example Output

```
ThreadsCpuManager Memory Breakdown:
  Base object size:        96 bytes
  Map storage:             1536 bytes (12 entries, 32 buckets)
  ThreadCpuInfo objects:   2048 bytes
  Total memory:            3680 bytes (3.59 KB)
```

#### Implementation Details

- Base object size
- Map bucket count and estimated overhead
- Sum of all `ThreadCpuInfo` objects including string capacities for thread names


### ExceptionsProvider

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `ExceptionsProvider` structure |
| Exception types cache | Map of ClassID → exception type name |
| GroupSampler | Internal sampling state (estimated) |

#### Example Output

```
ExceptionsProvider Memory Breakdown:
  Base object size:        256 bytes
  Exception types map:     2048 bytes (15 entries, 64 buckets)
  Exception type strings:  1280 bytes
  GroupSampler (estimate): 3200 bytes
  Total memory:            6784 bytes (6.63 KB)
```

#### Implementation Details

- Base object size
- Exception type cache with string capacities
- GroupSampler memory is estimated (contains internal maps)


### HeapSnapshotManager

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `HeapSnapshotManager` structure |
| Class histogram | Map of ClassID → ClassHistogramEntry (type names, counts, sizes) |

#### Example Output

```
HeapSnapshotManager Memory Breakdown:
  Base object size:        384 bytes
  Histogram map storage:   8192 bytes (250 entries, 512 buckets)
  ClassHistogramEntry:     45000 bytes
  Total memory:            53576 bytes (52.32 KB)
```

#### Implementation Details

- Base object size
- Histogram map storage with bucket estimates
- Sum of all `ClassHistogramEntry` objects including string capacities for class names


### NetworkProvider

#### What Gets Measured

| Component | Description |
|-----------|-------------|
| Base object | Size of `NetworkProvider` structure |
| Requests map | Map of NetworkActivity → NetworkRequestInfo (URLs, errors, callstacks) |

#### Example Output

```
NetworkProvider Memory Breakdown:
  Base object size:        192 bytes
  Requests map storage:    4096 bytes (8 entries, 64 buckets)
  NetworkRequestInfo:      12288 bytes
  Total memory:            16576 bytes (16.19 KB)
```

#### Implementation Details

- Base object size
- Requests map with bucket estimates
- Sum of all `NetworkRequestInfo` objects including:
  - String capacities (URL, errors)
  - Redirect information if present
  - Callstack sizes



## Future Components

Memory measurement should be added to:

- **Profile/ProfileImpl** - Accumulated profile data between exports (requires libdatadog API additions)
- **Other data structures** as needed

---

*Last updated: 2026-02-03*
