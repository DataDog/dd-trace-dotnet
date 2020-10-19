namespace Samples.ExampleLibrary.FakeClient
{
    public class DogTrick<T>
    {
        public string Message { get; set; }
        public T Reward { get; set; }
    }

    public class DogTrick
    {
        public string Message { get; set; }
    }
}
