namespace BenchmarkFramework;

[AttributeUsage(AttributeTargets.Method)]
public class BenchmarkAttribute(string description) : Attribute
{
    public bool IsBaseline { get; set; }

    public string? Description { get; } = description;
}
