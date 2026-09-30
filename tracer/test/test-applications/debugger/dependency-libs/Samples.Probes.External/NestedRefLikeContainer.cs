namespace Samples.Probes.External
{
    /// <summary>
    /// Nested ref struct in a different assembly from the instrumented method.
    /// Used to verify Dynamic Instrumentation skips nested byref-like TypeRefs.
    /// </summary>
    public class NestedRefLikeContainer
    {
        public readonly ref struct NestedRefLike
        {
            public NestedRefLike(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
