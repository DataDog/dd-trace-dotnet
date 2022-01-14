## Automatic Instrumentation

The _ClrProfiler_ folder contains the majority of code required for automatic instrumentation of target methods. Since v2.0.0, we exclusively use "Call Target" modification, in which we rewrite the target method to add our instrumentation. 

### Creating a new automatic instrumentation implementation

Creating a new instrumentation implementation typically uses the following process:

1. Identify the operation of interest that we want to measure
2. Find an appropriate instrumentation point in the target library. You may need to use multiple instrumentation points, and you may need to use different targets for different _versions_ of the library
3. Create an instrumentation class using one of the standard "shapes" (described below), and place it in the [ClrProfiler/AutoInstrumentation folder](./AutoInstrumentation).
4. Add an `[InstrumentMethod]` attribute to the instrumentation class, as described below. Alternatively, add an assembly-level `[AdoNetClientInstrumentMethods]` attribute
5. (Optional) Create duck-typing unit tests in _Datadog.Trace.Tests_ to confirm any duck types are valid. This can make the feedback cycle much faster than relying on integration tests
6. Create integration tests for your instrumentation 
   1. Create (or reuse) a sample application that uses the target library, which ideally exercises all the code paths in your new instrumentation. Use an `$(ApiVersion)` MSBuild variables to allow testing against multiple package versions in CI. 
   2. Add an entry in [tracer/build/PackageVersionsGeneratorDefinitions.json](../../../build/PackageVersionsGeneratorDefinitions.json) defining the range of all supported versions. See the existing definitions for examples
   3. Run `./tracer/build.ps1 GeneratePackageVersions`. This generates the xunit test data for package versions in the `TestData` that you can use as `[MemberData]` for your `[Theory]` tests. 
   4. Use the `MockTracerAgent` to confirm your instrumentation is working as expected.
7. After testing locally, push to GitHub, and do a manual run in Azure Devops for your branch
   1. Navigate to the [consolidated-pipeline](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build?definitionId=54)
   2. Click `Run Pipeline`
   3. Select your branch from the drop down
   4. Click `Variables`, set `perform_comprehensive_testing` to true. (This is false for PRs by default for speed, but ensures your new code is tested against all the specified packages initially)
   5. Select `Stages To Run`, and select only the `build*`, `unit_test*` and `integration_test*` stages. This avoids using excessive resources, and will complete your build faster
8. Once your test branch works, create a PR!

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
    
    // 👇 Async method
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
    
    // 👇 Method with return value TReturn
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

### Instrumentation attributes

A source generator is used to automatically "find" all custom instrumentation classes in the app and generate a list of them to pass to the native CLR profiler. We do this by using one of two attributes:

- [`[InstrumentMethod]`](./InstrumentMethodAttribute.cs)
- [`[AdoNetClientInstrumentMethods]`](./AutoInstrumentation/AdoNet/AdoNetClientInstrumentMethodsAttribute.cs)

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

We also have a special case for ADO.NET method instrumentation, as this is generally more convoluted, and requires a lot of duplication. All new ADO.NET implementations will likely reuse existing instrumentation classes, such as [`CommandExecuteReaderIntegration`](./AutoInstrumentation/AdoNet/CommandExecuteReaderIntegration.cs) for example. To save having to specify many `[InstrumentMethod]` attributes, you can instead use the  `[AdoNetClientInstrumentMethods]` _assembly_ attribute, to define some standard types, as well as which of the standard ADO.NET signatures to implement. For example:

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

The above attribute shows how to select which signatures to implement, via the  `TargetMethodAttributes` property. These attributes are nested types defined inside [`AdoNetClientInstrumentMethodsAttribute`](./AutoInstrumentation/AdoNet/AdoNetClientInstrumentMethodsAttribute.cs), each of which are associated with a given signature + instrumentation class (via the `[AdoNetClientInstrumentMethodsAttribute.AdoNetTargetSignature]` attribute)

> Note that there are separate target method attributes if you are using the new abstract/interface instrumentation feature.