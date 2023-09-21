using System;
using System.Runtime.InteropServices;
using DeepNestedHierarchy;

if (!Samples.SampleHelpers.IsProfilerAttached())
{
    Console.WriteLine("Error: Profiler is required and is not loaded.");
    return 1;
}

#if NETCOREAPP
Console.WriteLine("Process details: ");
Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Process arch: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"OS arch: {RuntimeInformation.OSArchitecture}");
Console.WriteLine($"OS description: {RuntimeInformation.OSDescription}");
Console.WriteLine($"IsArchive: {RuntimeInformation.OSDescription}");
#endif

Console.WriteLine("Creating trace");

using (Samples.SampleHelpers.CreateScope("Test scope"))
{
}

Console.WriteLine("App completed successfully");
return 0;

// These types are placeholders to build a deep nested hierarchy,
// based on the one found in Microsoft.AspNetCore.OData
// https://github.com/OData/WebApi/blob/8c03a9077020859096a65a49bcc6e6e68429ab98/src/Microsoft.AspNet.OData.Shared/Query/Expressions/PropertyContainer.generated.cs

internal class AutoSelectedNamedProperty<T> : NamedProperty<T>
{
}

internal class NamedProperty<T> : PropertyContainer
{
}

internal class SingleExpandedProperty<T> : NamedProperty<T>
{
}

internal class CollectionExpandedProperty<T> : NamedProperty<T>
{
    
}
