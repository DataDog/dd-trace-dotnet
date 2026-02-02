# Why Duck Typing Failed for MassTransit SendHeaders

## Problem Statement

We attempted to replace reflection-based method invocation with duck typing in the `SendContextHeadersAdapter` class to improve performance when injecting trace context headers into MassTransit messages. However, duck typing failed to work with MassTransit's `DictionarySendHeaders` class.

## Root Cause

MassTransit's `DictionarySendHeaders` class implements the `Set` methods using **explicit interface implementation**, which means the methods are **private** on the concrete class and only accessible through the interface.

### Evidence from Assembly Inspection

Using reflection to inspect the MassTransit.dll assembly (v7.3.1), we found:

```
Type: MassTransit.Context.DictionarySendHeaders

=== Public Instance Methods named 'Set' ===
(empty - no public methods)

=== All Methods (including private) ===
  Void MassTransit.SendHeaders.Set(System.String, System.String)
  Void MassTransit.SendHeaders.Set(System.String, System.Object, Boolean)

=== Interfaces ===
  MassTransit.SendHeaders
    Interface methods:
      Void Set(String key, String value)
      Void Set(String key, Object value, Boolean overwrite = True)
```

**Key Observation:** The `Set` methods exist only as **explicit interface implementations** (prefixed with `MassTransit.SendHeaders.`), not as public instance methods on the class itself.

## Duck Typing Limitation

The Datadog duck typing system looks for methods on the **concrete class**, not on explicitly implemented interface methods. This is a fundamental limitation of duck typing in .NET:

1. **Explicitly implemented interface methods** are compiled as private methods with the full interface name as a prefix
2. These methods are **not accessible via normal reflection** using `Type.GetMethod()` without knowing the exact interface
3. Duck typing uses `Type.GetMethod()` and cannot find these methods on the concrete type

## Attempted Solutions

### Attempt 1: `Set(string, object, bool)`
```csharp
internal interface ISendHeaders
{
    void Set(string key, object value, bool overwrite);
}
```
**Result:** `DuckTypeTargetMethodNotFoundException: The target method for the proxy method 'Void Set(System.String, System.Object, Boolean)' was not found.`

### Attempt 2: `Set(string, string)`
```csharp
internal interface ISendHeaders
{
    void Set(string key, string value);
}
```
**Result:** `DuckTypeTargetMethodNotFoundException: The target method for the proxy method 'Void Set(System.String, System.String)' was not found.`

### Attempt 3: `[DuckReverseMethod]` Attribute
```csharp
internal interface ISendHeaders
{
    [DuckReverseMethod(ParameterTypeNames = new[] { "System.String", "System.String" })]
    void Set(string key, string value);
}
```
**Result:** `DuckTypeIncorrectReverseMethodUsageException: The method 'Set' was marked as a [DuckReverseMethod] but not doing reverse duck typing.`

The `DuckReverseMethod` attribute is for **reverse duck typing** (duck typing FROM a concrete type TO an interface), not for finding explicitly implemented interface methods.

## Why Reflection Still Works

The original reflection-based approach works because it:

1. Uses `Type.GetInterfaces()` to enumerate all implemented interfaces
2. Searches for interfaces with names containing "Headers"
3. Uses `Type.GetInterfaceMap()` to map interface methods to concrete implementations
4. Retrieves the actual `MethodInfo` for the explicitly implemented method
5. Caches the `MethodInfo` for reuse
6. Invokes the method using `MethodInfo.Invoke()`

This approach can find explicitly implemented interface methods because it specifically looks through the interface map, not just the public methods on the class.

## Comparison: Why IHeaders Works But ISendHeaders Doesn't

The extraction code uses `IHeaders` duck typing successfully:
```csharp
internal interface IHeaders
{
    System.Collections.IEnumerable GetAll();
}
```

This works because the concrete types that implement `IHeaders` (like `JsonTransportHeaders`) likely implement the `GetAll()` method as a **public instance method**, not an explicit interface implementation.

### Deep Dive: Explicit vs Implicit Interface Implementation in .NET

In C#, there are two ways to implement interface methods:

#### 1. Implicit Implementation (Public Methods)
```csharp
public class MyHeaders : IHeaders
{
    // Public method - accessible both through the class AND the interface
    public IEnumerable GetAll()
    {
        // ...
    }
}
```

With implicit implementation:
- The method is **public** on the class
- Can be called directly: `myHeaders.GetAll()`
- Can be called through interface: `((IHeaders)myHeaders).GetAll()`
- **Duck typing works** - `Type.GetMethod("GetAll")` finds it

#### 2. Explicit Interface Implementation (Private Methods)
```csharp
public class DictionarySendHeaders : ISendHeaders
{
    // Private method with full interface name - ONLY accessible through the interface
    void ISendHeaders.Set(string key, string value)
    {
        // ...
    }
}
```

With explicit implementation:
- The method is **private** on the class (with interface prefix in IL)
- **Cannot** be called directly: `headers.Set(...)` won't compile
- Can **only** be called through interface: `((ISendHeaders)headers).Set(...)`
- **Duck typing fails** - `Type.GetMethod("Set")` returns null
- The IL method name is `ISendHeaders.Set`, not just `Set`

### Why MassTransit Uses Explicit Implementation

MassTransit uses explicit implementation for `SendHeaders.Set` methods, likely because:

1. **Avoiding name collisions** - The class might have its own `Set` method for internal use
2. **Intentional design** - Forces consumers to use the interface, not the concrete class
3. **Multiple interface implementation** - The class might implement multiple interfaces with conflicting method names

### Why Duck Typing Can't Handle Explicit Implementation

Duck typing in the Datadog tracer uses `Type.GetMethod()` to find methods by name:

```csharp
// This works for implicit implementation
var method = type.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Instance);

// This returns NULL for explicit implementation
var method = type.GetMethod("Set", BindingFlags.Public | BindingFlags.Instance);
// Returns null because "Set" is private, real name is "ISendHeaders.Set"
```

To find explicitly implemented methods, you need:

```csharp
// Get the interface
var iface = type.GetInterface("ISendHeaders");

// Get the method from the interface
var interfaceMethod = iface.GetMethod("Set");

// Map it to the concrete implementation
var map = type.GetInterfaceMap(iface);
var concreteMethod = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
```

This is exactly what our reflection code does! But duck typing doesn't have this logic built-in.

### Summary Table

| Aspect | Implicit Implementation | Explicit Implementation |
|--------|------------------------|-------------------------|
| Method visibility | Public | Private |
| Callable via class | ✅ Yes | ❌ No |
| Callable via interface | ✅ Yes | ✅ Yes |
| Found by `GetMethod()` | ✅ Yes | ❌ No |
| Duck typing works | ✅ Yes | ❌ No |
| Requires `GetInterfaceMap()` | ❌ No | ✅ Yes |
| Example | `IHeaders.GetAll()` | `ISendHeaders.Set()` |

## Conclusion

Duck typing cannot be used for MassTransit's `SendHeaders.Set` methods because:

1. The methods are explicitly implemented on the `MassTransit.SendHeaders` interface
2. Duck typing cannot find explicitly implemented interface methods on concrete types
3. The `DuckReverseMethod` attribute is not designed for this use case

**Recommendation:** Keep the reflection-based approach for `SendContextHeadersAdapter`. While reflection has some overhead, it's already optimized by:
- Caching the `MethodInfo` lookup (done once per adapter instance)
- Only invoking the method via reflection (not repeatedly looking it up)
- The overhead is minimal compared to the network/messaging overhead

## Performance Impact

The reflection approach does the following per message send:
1. **Constructor** (one-time per adapter):
   - `GetType()`: 1 call
   - `GetInterfaces()`: 1 call
   - `GetMethod()`: 2-4 calls (until match found)
   - `GetInterfaceMap()`: 1-2 calls

2. **Per Set call**:
   - `MethodInfo.Invoke()`: 1 call

The duck typing approach would have been:
1. **Constructor** (one-time):
   - `DuckCast<T>()`: 1 call

2. **Per Set call**:
   - Direct method call: 0 reflection

However, since duck typing is impossible for this use case, reflection remains the only viable option.

## Date
2026-02-02

## Author
Investigation conducted during MassTransit instrumentation optimization
