using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Wcf.Server;

class HttpCalculator : IHttpCalculator
{
    public double ServerSyncAddJson(string n1, string n2) => GetResult(n1, n2);

    public double ServerSyncAddXml(string n1, string n2) => GetResult(n1, n2);

    public async Task<double> ServerTaskAddPost(CalculatorArguments arguments)
    {
        await Task.Yield();
        return GetResult(arguments.Arg1.ToString(), arguments.Arg2.ToString());
    }

    public double ServerSyncAddWrapped(string n1, string n2) => GetResult(n1, n2);

    private static double GetResult(string n1, string n2, [CallerMemberName] string member = null)
    {
        LoggingHelper.WriteLineWithDate($"[Server] Received {member}({n1},{n2})");
        var result = double.Parse(n1) + double.Parse(n2);

        LoggingHelper.WriteLineWithDate($"[Server] Return {member}: {result}");
        return result;
    }
}
