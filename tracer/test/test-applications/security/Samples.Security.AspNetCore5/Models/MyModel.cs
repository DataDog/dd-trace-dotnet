namespace Samples.Security.AspNetCore5.Models
{
    public class MyModel
    {
        public string Property { get; set; }
        public string Property2 { get; set; }
        public int Property3 { get; set; }
        public int Property4 { get; set; }

        public override string ToString() => $"{Property}, {Property2}, {Property3}, {Property4}";

    }
}
