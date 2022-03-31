namespace Samples.AspNetMvc5.Models
{
    public struct MiscModelStruct
    {
        public string Property1 { get; set; }
        public string Property2 { get; set; }

        public override string ToString()
        {
            return $"MiscModelStruct - Property1 : {Property1}, Property2 : {Property2}";
        }
    }
}
