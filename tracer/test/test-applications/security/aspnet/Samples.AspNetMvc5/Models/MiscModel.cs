namespace Samples.AspNetMvc5.Models
{
    public class MiscModel
    {
        public string Property1 { get; set; }
        public string Property2 { get; set; }

        public override string ToString()
        {
            return $"Property1 : {Property1}, Property2 : {Property2}";
        }
    }
}
