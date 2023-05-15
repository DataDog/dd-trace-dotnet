## Automatic Instrumentation

The _ClrProfiler_ folder contains the majority of code required for automatic instrumentation of target methods. Since v2.0.0, we exclusively use "Call Target" modification, in which we rewrite the target method to add our instrumentation. 

### Creating a new automatic instrumentation implementation

Creating a new instrumentation implementation typically uses the following process:

1. Identify the operation of interest that we want to measure. Also gather the tags, resource names that we will need to set. Don't forget to check what has been implemented by other tracers.
2. Find an appropriate instrumentation point in the target library. You may need to use multiple instrumentation points, and you may need to use different targets for different _versions_ of the library
3. Create an instrumentation class using one of the standard "shapes" (described below), and place it in the [ClrProfiler/AutoInstrumentation folder](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation). If the methods you need to instrument have different prototypes (especially the number of parameters), you will need multiple class to instrument them.
4. Add an `[InstrumentMethod]` attribute to the instrumentation class, as described below. Alternatively, add an assembly-level `[AdoNetClientInstrumentMethods]` attribute
5. (Optional) Create duck-typing unit tests in _Datadog.Trace.Tests_ to confirm any duck types are valid. This can make the feedback cycle much faster than relying on integration tests
6. Create integration tests for your instrumentation 
   1. Create (or reuse) a sample application that uses the target library, which ideally exercises all the code paths in your new instrumentation. Use an `$(ApiVersion)` MSBuild variables to allow testing against multiple package versions in CI. 
   2. Add a new entry in the SpanMetadataRules files (see the /tracer/test/Datadog.Trace.TestHelpers/SpanMetadata*Rules.cs files) that define the expected Name, Type, and Tags for the new integration spans, and run build target `GenerateSpanDocumentation` to generate the updated Markdown file. For new instrumentation, you should add the definitions for all existing schema versions.
   3. Add an entry in [tracer/build/PackageVersionsGeneratorDefinitions.json](../../tracer/build/PackageVersionsGeneratorDefinitions.json) defining the range of all supported versions. See the existing definitions for examples. You may need to add an entry in the [tracer/build/Honeypot/IntegrationGroups.cs](../../tracer/build//Honeypot/IntegrationGroups.cs) to specify the Nuget Package instrumented by the integration. 
   4. Run `./tracer/build.ps1 GeneratePackageVersions`. This generates the xunit test data for package versions in the `TestData` that you can use as `[MemberData]` for your `[Theory]` tests. 
   5. If needed, add a docker image in the docker-compose.yml to allow the CI to test against it. Locally, you can use docker-compose as well and start only the dependencies you need.
   6. Use the `MockTracerAgent` and the newly defined `SpanMetadataRules` method in your integration test to confirm your instrumentation is working as expected.
7. After testing locally, push to GitHub, and do a manual run in Azure Devops for your branch
   1. Navigate to the [consolidated-pipeline](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build?definitionId=54)
   2. Click `Run Pipeline`
   3. Select your branch from the drop down
   4. Click `Variables`, set `perform_comprehensive_testing` to true. (This is false for PRs by default for speed, but ensures your new code is tested against all the specified packages initially)
   5. Select `Stages To Run`, and select only the `build*`, `unit_test*` and `integration_test*` stages. This avoids using excessive resources, and will complete your build faster
   6. Add the instrumentation to the list of integrations in the [dotnet-core tracing documentation](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core/#integrations) and/or [dotnet-framework tracing documentation](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-framework/#integrations) as appropiate.
   7. Once your test branch works, create a PR for both the `dd-trace-dotnet` and `documentation` repositories and have them reference each other!

### Instrumentation classes

When implementing instrumentation classes, you can run code both _before_ the target method is entered, and _after_ it is entered. Your `OnMethodBegin` method will always look the same, but the shape of the `OnMethodEnd` depends on whether the method is async, and whether it returns void:

```csharp
public class ClientQueryIteratorsIntegrations
{
    // The parameters here should match the method signature of the target method
    // Use generic parameters for non-BCL types that you can't directly reference 
    internal static CallTargetState OnMethodBegin<TTarget, TOther>(TTarget instance, TOther otherParam)
    {
        // Run your "method start" code here
    }
    
    // Include ONE of the following:
    
    // 👇 Async method, with different behavior based on the return type
    //   - Task<TReturn>: returnValue is set to the Result
    //   - Task: TReturn is set to typeof(object) and returnValue is set to null
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
    
    // 👇 Method with return type TReturn
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // Run your "method end" code here
    }
    
    // 👇 Void method
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        // Run your "method end" code here
    }
}
```

> Note that both `OnMethodBegin` and `OnMethodEnd` are optional. If you only need one of the integration points, you can omit the others

#### `OnMethodEnd` and `OnMethodBegin` parameters
 
The first parameter passed to the method is the instance on which the method is called (for `static` methods, this parameter should be omitted), and should be a _generic parameter_ type.  

For parameters that are well-known types like `string`, `object`, or `Exception`, you can use the type directly in the `OnMethodBegin` or `OnMethodEnd` methods. For other types that can't be directly referenced, such as types in the target-library, you should use generic parameters. If you need to manipulate the generic parameters, for example to access values, use the duck-typing approach described below.



##### `OnMethodBegin`

OnMethodBegin signatures with 1 or more parameters with 1 or more generics:
```csharp
      CallTargetState OnMethodBegin<TTarget>(TTarget instance);
      CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg1);
      CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, ref TArg1 arg1, ref TArg2);
      CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, ...>(TTarget instance, ref TArg1 arg1, ref TArg2, ...);
      CallTargetState OnMethodBegin<TTarget>();
      CallTargetState OnMethodBegin<TTarget, TArg1>(ref TArg1 arg1);
      CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(ref TArg1 arg1, ref TArg2);
      CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, ...>(ref TArg1 arg1, ref TArg2, ...)
```
The last four signatures are for static classes.
> For performance reasons, it is recommended to use the `ref` or `in` (if there's no need to edit the argument) keyword in front of the arguments after the instance one. Note that you cannot use `in` or `ref` if you are using duck-typing constraints on the parameters ([which you should be, where possible](../DuckTyping.md#best-practices)).


##### `OnMethodEnd`

The penultimate parameter passed must be of type `System.Exception`: it's the potential exception that could have been thrown in the instrumented body's method.
 
Here are the patterns which can be matched:

OnMethodEnd signatures with 2 or 3 parameters with 1 generics:
```csharp
      CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state);
      CallTargetReturn OnMethodEnd<TTarget>(Exception exception, CallTargetState state);
      CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state);
      CallTargetReturn OnMethodEnd<TTarget>(Exception exception, in CallTargetState state);
```

 OnMethodEnd signatures with 3 or 4 parameters with 1 or 2 generics:
```csharp
      CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state);
      CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, CallTargetState state);
      CallTargetReturn<[Type]> OnMethodEnd<TTarget>([Type] returnValue, Exception exception, CallTargetState state);
      CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state);
      CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state);
      CallTargetReturn<[Type]> OnMethodEnd<TTarget>([Type] returnValue, Exception exception, in CallTargetState state);
```

OnAsyncMethodEnd signatures with 3 or 4 parameters with 1 or 2 generics:
```csharp
      TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state);
      TReturn OnAsyncMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, CallTargetState state);
      [Type] OnAsyncMethodEnd<TTarget>([Type] returnValue, Exception exception, CallTargetState state);
      TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state);
      TReturn OnAsyncMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state);
      [Type] OnAsyncMethodEnd<TTarget>([Type] returnValue, Exception exception, in CallTargetState state);
```
In case the continuation is for a `Task` or `ValueTask`, the returnValue type will be an object and the value `null`.
In case the continuation is for a `Task<T>` or `ValueTask<T>`, the returnValue type will be `T` with the instance value after the task completes.

> For performance reasons, it is recommended to use the `in` keyword in front of the `CallTargetState state` parameter.


### Duck-typing, instrumentation classes, and constraints

When creating instrumentation classes you often need to work with `Type`s in the target library that you can't reference directly. Rather than using reflection directly to manipulate these types, the .NET Tracer has an optimised solution for working with them called _Duck Typing_.

> See [the Duck Typing document](./DuckTyping.md) for a detailed description of duck-typing, use cases, best practices,  and benchmarks.
 
You can use duck-typing imperatively at runtime to "cast" any object to a type you can manipulate directly, but if you know at call time that you need to work with one of the generic parameters passed to an `OnMethodBegin` or `OnMethodEnd`, you can use a more performant approach leveraging _constraints_. 

Add a constraint to your method that the generic type implements your duck type. The value passed to your method will then be pre-duck-typed, and will have better performance than using duck-typing manually at a later point. For example, the integration below [(A GraphQL integration)](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/GraphQL/ExecuteAsyncIntegration.cs) takes a single parameter which is duck-typed using constraints to implement the type IExecutionContext

```csharp
public class ExecuteAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
        where TContext : IExecutionContext
    {
        // ...
    }
```

> ⚠ Note that pre duck-typed parameters that use constraints will never be `null`. If you need to check the parameter for `null`, add the `IDuckType` constraint too, and check the value of `IDuckType.Instance`. 

For more information on duck-typing, see [the documentation](./DuckTyping.md).

### Instrumentation attributes

A source generator is used to automatically "find" all custom instrumentation classes in the app and generate a list of them to pass to the native CLR profiler. We do this by using one of two attributes:

- [`[InstrumentMethod]`](../../tracer/src/Datadog.Trace/ClrProfiler/InstrumentMethodAttribute.cs)
- [`[AdoNetClientInstrumentMethods]`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/AdoNetClientInstrumentMethodsAttribute.cs)

> Alternatively, you can _manually_ call `NativeMethods.InitializeProfiler()`, passing in `NativeCallTargetDefinition[]`. This is not the "normal" approach, but may be necessary when you need to dynamically generate definitions, for example in serverless scenarios

In most cases, you will want `[InstrumentMethod]`. You apply this to your instrumentation class, describing the target assembly, method, instrumentation name etc. For example:

```csharp
 [InstrumentMethod(
    AssemblyName = "System.Net.Http",
    TypeName = "System.Net.Http.HttpClientHandler",
    MethodName = "SendAsync",
    ReturnTypeName = ClrNames.HttpResponseMessageTask,
    ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.CancellationToken },
    MinimumVersion = "4.0.0",
    MaximumVersion = "6.*.*",
    IntegrationName = IntegrationName)]
public class HttpClientHandlerIntegration
{
    // ...
}
```

We also have a special case for ADO.NET method instrumentation, as this is generally more convoluted, and requires a lot of duplication. All new ADO.NET implementations will likely reuse existing instrumentation classes, such as [`CommandExecuteReaderIntegration`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/CommandExecuteReaderIntegration.cs) for example. To save having to specify many `[InstrumentMethod]` attributes, you can instead use the  `[AdoNetClientInstrumentMethods]` _assembly_ attribute, to define some standard types, as well as which of the standard ADO.NET signatures to implement. For example:

```csharp
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySql.Data",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "6.7.0",
    MaximumVersion = "6.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>",
    TargetMethodAttributes = new[]
    {
        // int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object MySql.Data.MySqlClient.MySqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]
```

The above attribute shows how to select which signatures to implement, via the  `TargetMethodAttributes` property. These attributes are nested types defined inside [`AdoNetClientInstrumentMethodsAttribute`](../../tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AdoNet/AdoNetClientInstrumentMethodsAttribute.cs), each of which are associated with a given signature + instrumentation class (via the `[AdoNetClientInstrumentMethodsAttribute.AdoNetTargetSignature]` attribute)

> Note that there are separate target method attributes if you are using the new abstract/interface instrumentation feature.
 
### Instrumentable methods 

#### Inheritance

> ⚠️ Warning: This requires much more overhead because all of the types in a module must be inspected to determine if a module requires instrumentation. Carefully consider when to introduce this type of instrumentation.

Virtual or normal methods in abstract classes can be instrumented, as long as they are not overridden. But if a class inherits and overrides those methods, then the original instrumentation won't be called. To make sure the child classes methods are instrumented, another property needs to be added specifying `Datadog.Trace.ClrProfiler.IntegrationType` as `IntegrationType.Derived`, but the same integration can be used since the methods will have the same signature. Example:

```csharp
    [InstrumentMethod(AssemblyName = "AssemblyName", TypeName = "AbstractType", MethodName = "MethodName" ...)]
    [InstrumentMethod(AssemblyName = "AssemblyName", TypeName = "AbstractType", MethodName = "MethodName", CallTargetIntegrationType = IntegrationType.Derived  ...)]
    public class My_Integration
    {
```
Note that only one level of depth is currently supported, i.e a child class of a child class of an abstract class won't be instrumented. 

#### Properties

Properties can be instrumented the same way as methods, but because of the way the compiler works and generates IL, the method name needs to be prefixed with `get_` or `set_`. E.g, for a string property called `Name`, those methods signatures are:

```csharp
string get_Name();
void set_Name(string value);
```

### Interfaces

> ⚠️ Warning: This requires much more overhead because all of the types in a module must be inspected to determine if a module requires instrumentation. Carefully consider when to introduce this type of instrumentation.

Interface methods can be instrumented by setting the `TypeName` to the name of the interface and setting `Datadog.Trace.ClrProfiler.IntegrationType` to `IntegrationType.Interface`. Instrumentation will occur on types that directly implement the specified interface. But if a class inherits and overrides those methods, then the original instrumentation won't be called. To make sure the child classes methods are instrumented, another property needs to be added that sets the `TypeName` to the base class and sets the `Datadog.Trace.ClrProfiler.IntegrationType` to `IntegrationType.Derived`. Example:

```csharp
    [InstrumentMethod(AssemblyName = "AssemblyName", TypeName = "InterfaceA", MethodName = "MethodName", CallTargetIntegrationType = IntegrationType.Interface ...)]
    [InstrumentMethod(AssemblyName = "AssemblyName", TypeName = "TypeThatImplementsInterfaceA", MethodName = "MethodName", CallTargetIntegrationType = IntegrationType.Derived  ...)]
    public class My_Integration
    {
```