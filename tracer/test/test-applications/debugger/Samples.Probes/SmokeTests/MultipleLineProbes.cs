namespace Samples.Probes.SmokeTests;

[LineProbeTestData(lineNumber: 9, skip:true)] 
[LineProbeTestData(lineNumber: 11, skip:true)]
public class MultipleLineProbes : IRun
{
    public void Run()
    {
        int a = 0;
        a++;
        a++;
        a++;
    }
}
