namespace AssemblyLoadContextRedirect
{
    public class StuffUsingTracer
    {
        public static void Invoke()
        {
            using var scope = Datadog.Trace.Tracer.Instance.StartActive("Hello");
        }
    }
}
