using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SpanDecorationAsync : IAsyncRun
    {
        private const string Json = @"
{
   ""Decorations"":[
      {
         ""When"":{
            ""Str"":null,
            ""Dsl"":null,
            ""Json"":{
                ""gt"":[
                    {
                        ""ref"": ""intLocal""
                    },
                    {
                        ""ref"": ""intArg""
                    }
                ]
            }
         },
         ""Tags"":[
            {
               ""Name"":""SpanDecorationAsync"",
               ""Value"":{
                  ""Template"":null,
                  ""Segments"":[
                    {
                        ""Str"":null,
                        ""Dsl"":null,
                        ""Json"":{
                            ""ref"": ""arg""
                        }
                    }
                  ]
               }
            }
         ]
      }
   ]
}
";

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData(skip: true /* TODO DEBUG-1912 */)]
        public async Task RunAsync()
        {
            Console.WriteLine(await Annotate(nameof(RunAsync), nameof(RunAsync).Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        async Task<string> Annotate(string arg, int intArg)
        {
            return await Method(arg, intArg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanDecorationMethodProbeTestData(decorationsJson: Json, skip: true /* TODO DEBUG-1912 */)]
        async Task<string> Method(string arg, int intArg)
        {
            var intLocal = nameof(Method).Length * 2;
            if (intLocal > intArg)
            {
                Console.WriteLine(intLocal);
                await Task.Delay(20);
            }

            return $"{arg} : {intLocal.ToString()}";
        }
    }
}
