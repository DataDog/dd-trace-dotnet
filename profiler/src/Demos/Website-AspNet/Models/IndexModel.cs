namespace Website_AspNet.Models
{
    public class IndexModel
    {
        public IndexModel(FibonacciData data)
        {
            ComputationData = data;
        }

        public FibonacciData ComputationData { get; }
    }
}
