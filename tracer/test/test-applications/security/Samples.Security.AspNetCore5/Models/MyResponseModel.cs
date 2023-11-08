namespace Samples.Security.AspNetCore5.Models;

public class MyResponseModel
{
    public string PropertyResponse { get; set; }
    public long PropertyResponse2 { get; set; }
    public double PropertyResponse3 { get; set; }
    public int PropertyResponse4 { get; set; }

    public override string ToString() => $"{PropertyResponse}, {PropertyResponse2}, {PropertyResponse3}, {PropertyResponse4}";
}
