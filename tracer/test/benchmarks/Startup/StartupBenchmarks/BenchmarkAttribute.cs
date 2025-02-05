using System;

namespace StartupBenchmarks;

[AttributeUsage(AttributeTargets.Method)]
public class BenchmarkAttribute : Attribute
{
    public bool IsBaseline { get; set; }

    public string Description { get; set; }

    public BenchmarkAttribute()
    {
    }

    public BenchmarkAttribute(string description)
    {
        Description = description;
    }
}
