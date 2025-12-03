// See https://aka.ms/new-console-template for more information
Console.WriteLine("FeatureFlags SDK Sample");

if (!Datadog.Trace.FeatureFlags.FeatureFlagsSdk.IsAvailable())
{
    Console.WriteLine($"<NOT INSTRUMENTED>");
    return;
}

Console.WriteLine($"<INSTRUMENTED>");

var evaluation = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate("DD_Enabled", typeof(string), "Not found", null);

if (evaluation is null)
{
    Console.WriteLine($"Eval (DD_Enabled) : <NULL>");
}
else if (evaluation.Error is not null)
{
    Console.WriteLine($"Eval (DD_Enabled) : <ERROR: {evaluation?.Error}>");
}
else 
{
    Console.WriteLine($"Eval (DD_Enabled) : <OK: {evaluation.Value ?? "<NULL>"}>");
}

