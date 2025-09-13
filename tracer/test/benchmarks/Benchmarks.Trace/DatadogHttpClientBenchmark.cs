using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent3]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class DatadogHttpClientBenchmark
    {
        private const string ResponseHeadersBaseString = @"HTTP/1.1 200 OK
Connection: keep-alive
Content-Length: 2468
Server: localhost
Content-Type: application/json; charset=utf-8
Last-Modified: Sun, 07 May 2023 11:05:35 GMT
Access-Control-Allow-Origin: *";

        private const string MinimalJson = "{}";
        private const string SmallJson = """
[
  {
    "_id": "68ac7c2524ff9ca8037a23c2",
    "index": 0,
    "guid": "c525b3b9-02b1-427b-90cd-66040d17c2b0",
    "isActive": true,
    "balance": "$2,139.60",
    "picture": "http://placehold.it/32x32",
    "age": 34,
    "eyeColor": "blue",
    "name": "Boone Wong",
    "gender": "male",
    "company": "FARMEX",
    "email": "boonewong@farmex.com",
    "phone": "+1 (977) 439-3919",
    "address": "776 Hooper Street, Morriston, New York, 9154",
    "about": "Labore officia esse est ex consectetur voluptate labore in veniam enim laborum. Id irure nostrud sunt culpa Lorem ea ut laboris amet. Lorem elit magna magna tempor dolore velit dolore. Irure consequat deserunt velit est duis voluptate exercitation sint dolor. In ea officia ipsum aute dolor ea nisi consequat ut. Anim incididunt tempor amet reprehenderit et veniam nisi officia consequat cillum in. Aliquip Lorem minim ad nostrud occaecat minim.\r\n",
    "registered": "2022-06-09T01:50:59 -02:00",
    "latitude": 65.498359,
    "longitude": -73.493292,
    "tags": [
      "est",
      "anim",
      "commodo",
      "non",
      "labore",
      "irure",
      "ipsum"
    ]
  }
]
""";
        private const string LargeJson = """
[
  {
    "_id": "68ac7cbbb1234f69e346854d",
    "index": 0,
    "guid": "c8c6f3f9-3b9d-4451-a4aa-e4bcb86fe652",
    "isActive": true,
    "balance": "$2,784.32",
    "picture": "http://placehold.it/32x32",
    "age": 21,
    "eyeColor": "green",
    "name": "Manuela Hale",
    "gender": "female",
    "company": "EXOSWITCH",
    "email": "manuelahale@exoswitch.com",
    "phone": "+1 (892) 416-2369",
    "address": "645 Bowne Street, Avoca, Puerto Rico, 136",
    "about": "Nostrud proident do sit amet. Laborum laborum nisi labore laboris ut ullamco reprehenderit deserunt eiusmod ex. Fugiat laboris proident cupidatat proident sint. Laboris quis eiusmod tempor labore ad et minim cupidatat consequat eiusmod aliquip quis anim officia. Tempor qui qui cillum ad commodo mollit.\r\n",
    "registered": "2023-10-17T11:35:22 -02:00",
    "latitude": 53.035356,
    "longitude": 67.60324,
    "tags": [
      "labore",
      "pariatur",
      "do",
      "do",
      "non",
      "cupidatat",
      "nisi"
    ]
  },
  {
    "_id": "68ac7cbb8316396692d1ee3f",
    "index": 1,
    "guid": "d6493773-e437-4527-8f19-e34b3d09b351",
    "isActive": true,
    "balance": "$2,233.81",
    "picture": "http://placehold.it/32x32",
    "age": 25,
    "eyeColor": "brown",
    "name": "Melody Osborn",
    "gender": "female",
    "company": "GEEKOLA",
    "email": "melodyosborn@geekola.com",
    "phone": "+1 (950) 502-3685",
    "address": "219 Terrace Place, Olney, Wyoming, 9976",
    "about": "Cillum laboris ut laboris magna exercitation ea commodo. Do do in ea sunt eiusmod ad culpa esse occaecat. Amet Lorem ea nulla fugiat irure. Id quis culpa aliquip incididunt commodo amet eu eiusmod eu consectetur ea qui cupidatat. Cupidatat voluptate in voluptate reprehenderit consequat do tempor proident officia proident anim id.\r\n",
    "registered": "2019-09-29T10:25:11 -02:00",
    "latitude": 20.824169,
    "longitude": 174.574734,
    "tags": [
      "sunt",
      "aute",
      "tempor",
      "veniam",
      "elit",
      "duis",
      "nisi"
    ]
  },
  {
    "_id": "68ac7cbb04f8e527899de5a4",
    "index": 2,
    "guid": "817545f9-da2e-4016-83d5-7ae5835a0e77",
    "isActive": true,
    "balance": "$1,965.63",
    "picture": "http://placehold.it/32x32",
    "age": 35,
    "eyeColor": "blue",
    "name": "Bryant Vargas",
    "gender": "male",
    "company": "JAMNATION",
    "email": "bryantvargas@jamnation.com",
    "phone": "+1 (850) 444-3797",
    "address": "652 Hale Avenue, Bethany, Florida, 3686",
    "about": "Cillum elit fugiat dolore laborum. Enim nostrud occaecat laboris deserunt est. Qui deserunt incididunt exercitation proident. Lorem cupidatat amet sint magna consequat nostrud velit culpa sit excepteur. Lorem veniam veniam enim adipisicing incididunt. Magna anim nostrud cillum velit adipisicing cupidatat eu elit sit esse occaecat duis.\r\n",
    "registered": "2025-04-12T06:37:28 -02:00",
    "latitude": -82.843058,
    "longitude": -74.736828,
    "tags": [
      "enim",
      "aute",
      "esse",
      "et",
      "laboris",
      "ipsum",
      "eiusmod"
    ]
  },
  {
    "_id": "68ac7cbb38431895d9d95611",
    "index": 3,
    "guid": "7d04d403-aff8-4875-93dc-2d4f1ce4f7d1",
    "isActive": false,
    "balance": "$2,101.93",
    "picture": "http://placehold.it/32x32",
    "age": 23,
    "eyeColor": "green",
    "name": "Tessa Bell",
    "gender": "female",
    "company": "KRAGGLE",
    "email": "tessabell@kraggle.com",
    "phone": "+1 (879) 441-2645",
    "address": "417 Just Court, Hinsdale, Marshall Islands, 3491",
    "about": "Est nulla quis velit tempor dolor reprehenderit deserunt ullamco et velit quis sit proident. Id labore irure enim veniam ad magna Lorem culpa magna dolor adipisicing et. Ipsum voluptate sunt ea enim aliquip tempor. In et ipsum reprehenderit adipisicing magna mollit quis dolor. Ad nostrud laboris amet aliquip exercitation.\r\n",
    "registered": "2016-08-24T02:11:14 -02:00",
    "latitude": 29.665143,
    "longitude": -39.249175,
    "tags": [
      "et",
      "Lorem",
      "nulla",
      "amet",
      "amet",
      "nulla",
      "quis"
    ]
  },
  {
    "_id": "68ac7cbb21d7780dfdda474b",
    "index": 4,
    "guid": "e73fe50b-ec9e-4ea2-b9cb-6596474f14ee",
    "isActive": true,
    "balance": "$3,920.42",
    "picture": "http://placehold.it/32x32",
    "age": 32,
    "eyeColor": "brown",
    "name": "Collier Mccall",
    "gender": "male",
    "company": "XTH",
    "email": "colliermccall@xth.com",
    "phone": "+1 (895) 444-3040",
    "address": "347 Eastern Parkway, Itmann, Missouri, 3850",
    "about": "Exercitation nulla nostrud incididunt labore aliquip eu aute cillum esse eiusmod voluptate ex et non. Velit excepteur et fugiat sunt incididunt sunt sint incididunt magna anim exercitation dolor enim elit. Cillum cupidatat Lorem incididunt in. Lorem proident Lorem excepteur laboris nulla consectetur id non non do adipisicing excepteur sunt. Reprehenderit non pariatur exercitation fugiat qui. Deserunt tempor voluptate magna culpa ipsum excepteur nulla culpa deserunt est ut. Sunt in eu non aliquip.\r\n",
    "registered": "2018-09-12T05:42:50 -02:00",
    "latitude": 33.432486,
    "longitude": -158.61714,
    "tags": [
      "anim",
      "laborum",
      "ex",
      "reprehenderit",
      "fugiat",
      "ut",
      "exercitation"
    ]
  },
  {
    "_id": "68ac7cbb3a653dcc28f81df0",
    "index": 5,
    "guid": "0a537e34-6e2a-41d7-ac8a-c69a1ef68e50",
    "isActive": false,
    "balance": "$2,487.94",
    "picture": "http://placehold.it/32x32",
    "age": 28,
    "eyeColor": "blue",
    "name": "Roslyn Jordan",
    "gender": "female",
    "company": "UTARIAN",
    "email": "roslynjordan@utarian.com",
    "phone": "+1 (905) 413-2272",
    "address": "615 Stewart Street, Imperial, Nevada, 4464",
    "about": "Reprehenderit proident magna qui duis velit labore occaecat mollit tempor cillum officia dolore eiusmod. Ea adipisicing cillum proident eu et. Occaecat laborum aliquip do do labore minim nulla anim deserunt magna elit aliqua. Aute cupidatat magna qui tempor eu labore ea commodo consequat consequat aliqua. Ex irure est laborum ut incididunt eu Lorem cillum duis eiusmod cupidatat officia esse.\r\n",
    "registered": "2019-03-18T07:04:24 -01:00",
    "latitude": -16.925678,
    "longitude": -1.009652,
    "tags": [
      "reprehenderit",
      "amet",
      "proident",
      "do",
      "ea",
      "culpa",
      "fugiat"
    ]
  },
  {
    "_id": "68ac7cbbaf9a89ed612b3b93",
    "index": 6,
    "guid": "6ff49e84-aec2-4eac-96cb-d4228a5ff77c",
    "isActive": false,
    "balance": "$1,759.13",
    "picture": "http://placehold.it/32x32",
    "age": 29,
    "eyeColor": "blue",
    "name": "Whitley Kline",
    "gender": "male",
    "company": "RODEOLOGY",
    "email": "whitleykline@rodeology.com",
    "phone": "+1 (839) 492-3458",
    "address": "906 Campus Place, Delshire, Idaho, 8834",
    "about": "Duis do reprehenderit id commodo in eu deserunt veniam. Esse Lorem sunt cupidatat eiusmod. Elit id velit minim veniam voluptate commodo. Nostrud est aliquip sunt dolor consequat irure cillum officia.\r\n",
    "registered": "2019-01-16T08:03:09 -01:00",
    "latitude": -7.23877,
    "longitude": -56.750896,
    "tags": [
      "cillum",
      "exercitation",
      "laborum",
      "dolore",
      "quis",
      "ea",
      "occaecat"
    ]
  },
  {
    "_id": "68ac7cbb86680f59c17e7412",
    "index": 7,
    "guid": "9c42035d-4095-4a76-ad37-0eb6d8ba278e",
    "isActive": true,
    "balance": "$3,471.86",
    "picture": "http://placehold.it/32x32",
    "age": 35,
    "eyeColor": "brown",
    "name": "Parsons Ware",
    "gender": "male",
    "company": "NURPLEX",
    "email": "parsonsware@nurplex.com",
    "phone": "+1 (927) 578-3745",
    "address": "463 Prospect Street, Muse, Michigan, 6822",
    "about": "Minim mollit enim et pariatur ad officia id dolore sint officia ipsum. Amet occaecat dolore consectetur amet commodo exercitation qui ut aute culpa. Minim enim dolore exercitation proident esse elit. Esse eiusmod voluptate pariatur cupidatat reprehenderit proident tempor eu laborum dolor sunt voluptate aliquip. Ullamco labore eiusmod tempor nulla occaecat voluptate est aliqua. Adipisicing enim fugiat culpa in laboris incididunt ex laborum sint.\r\n",
    "registered": "2019-02-26T07:53:03 -01:00",
    "latitude": 61.622883,
    "longitude": 96.832783,
    "tags": [
      "fugiat",
      "ut",
      "enim",
      "ullamco",
      "sunt",
      "in",
      "id"
    ]
  },
  {
    "_id": "68ac7cbb2df0b668503b41fa",
    "index": 8,
    "guid": "bb39c9ba-5f9a-4fa0-88d6-06bc5598e88b",
    "isActive": false,
    "balance": "$1,740.38",
    "picture": "http://placehold.it/32x32",
    "age": 40,
    "eyeColor": "brown",
    "name": "Taylor Humphrey",
    "gender": "female",
    "company": "REMOLD",
    "email": "taylorhumphrey@remold.com",
    "phone": "+1 (929) 496-3147",
    "address": "581 Norwood Avenue, Camino, North Dakota, 6119",
    "about": "Culpa officia qui magna labore nulla voluptate commodo laborum amet elit. Proident ad irure non anim qui pariatur commodo. Officia proident ullamco aute esse sit ad ad qui ex eu laboris ipsum. Cillum aute consectetur magna ut culpa sit sit nostrud cupidatat magna. Et cupidatat pariatur et et ad.\r\n",
    "registered": "2019-08-10T04:17:46 -02:00",
    "latitude": 6.749724,
    "longitude": 131.071709,
    "tags": [
      "labore",
      "nulla",
      "proident",
      "in",
      "mollit",
      "in",
      "dolor"
    ]
  },
  {
    "_id": "68ac7cbb194484dad1b0c170",
    "index": 9,
    "guid": "d8f4aa8c-febe-4613-ba3c-695176d2ace7",
    "isActive": true,
    "balance": "$1,870.76",
    "picture": "http://placehold.it/32x32",
    "age": 20,
    "eyeColor": "green",
    "name": "Blake Norman",
    "gender": "male",
    "company": "KONGLE",
    "email": "blakenorman@kongle.com",
    "phone": "+1 (854) 598-2524",
    "address": "662 Hewes Street, Belleview, Maine, 8527",
    "about": "Laboris est in veniam officia dolore eiusmod. Officia laboris eu aliquip dolor id velit quis incididunt in cillum culpa Lorem enim. Aliqua irure excepteur ad tempor exercitation esse ea laborum cupidatat est occaecat culpa enim elit. Qui proident ullamco laborum proident do pariatur pariatur eu aliqua duis. Voluptate adipisicing anim non ea adipisicing duis ullamco sunt reprehenderit ullamco ipsum. Ex dolor labore consectetur non Lorem pariatur ex.\r\n",
    "registered": "2016-04-15T01:19:58 -02:00",
    "latitude": -63.629992,
    "longitude": -41.584692,
    "tags": [
      "aute",
      "ut",
      "occaecat",
      "anim",
      "dolor",
      "do",
      "nisi"
    ]
  }
]
""";
        private static readonly DatadogHttpClient _client;
        private static readonly MemoryStream _requestStream;
        private static readonly MemoryStream _responseStreamBodyMinimal;
        private static readonly MemoryStream _responseStreamBodySmall;
        private static readonly MemoryStream _responseStreamBodyLarge;
        private static readonly MemoryStream _responseStreamHeadersBase;
        private static readonly MemoryStream _responseStreamHeadersPlus40;
        private static readonly MemoryStream _responseStreamHeadersPlus80;
        private static readonly MemoryStream _responseStreamHeadersPlus120;
        private static readonly HttpRequest _request;
        private static readonly byte[] _outBuffer;

        static DatadogHttpClientBenchmark()
        {
            _client = new DatadogHttpClient(new TraceAgentHttpHeaderHelper());
            var requestContent = new Datadog.Trace.HttpOverStreams.HttpContent.BufferContent(new ArraySegment<byte>(new byte[0]));
            _requestStream = new MemoryStream();
            _responseStreamBodyMinimal = ConvertStringToMemoryStream(ResponseHeadersBaseString + Environment.NewLine + Environment.NewLine + MinimalJson);
            _responseStreamBodySmall = ConvertStringToMemoryStream(ResponseHeadersBaseString  + Environment.NewLine + Environment.NewLine + SmallJson);
            _responseStreamBodyLarge = ConvertStringToMemoryStream(ResponseHeadersBaseString  + Environment.NewLine + Environment.NewLine + LargeJson);
            _responseStreamBodyLarge = ConvertStringToMemoryStream(ResponseHeadersBaseString  + Environment.NewLine + Environment.NewLine + LargeJson);
            _responseStreamHeadersBase = ConvertStringToMemoryStream(ResponseHeadersBaseString  + Environment.NewLine + Environment.NewLine + MinimalJson);
            _responseStreamHeadersPlus40 = ConvertStringToMemoryStream(ResponseHeadersBaseString + MakeHeaders(40) + Environment.NewLine + Environment.NewLine + MinimalJson);
            _responseStreamHeadersPlus80 = ConvertStringToMemoryStream(ResponseHeadersBaseString + MakeHeaders(80) + Environment.NewLine + Environment.NewLine + MinimalJson);
            _responseStreamHeadersPlus120 = ConvertStringToMemoryStream(ResponseHeadersBaseString + MakeHeaders(120)  + Environment.NewLine + Environment.NewLine + MinimalJson);
// GET / HTTP/1.1
// Host: localhost
// User-Agent: curl/8.14.1
// Accept: */*";
            var headers = new HttpHeaders()
            {
                { "Host", "localhost" },
                { "User-Agent", "curl/8.14.1" },
                { "Accept", "*/*" },
            };
            _request = new HttpRequest("GET", "localhost", "/", headers, requestContent);

            _outBuffer = new byte[12_000];
        }

        static string MakeHeaders(int headers)
        {
            var sb = new StringBuilder();
            for(int i = 0; i < headers; i++)
            {
                sb.AppendLine($"x-this-header-{i}: this-value-{i}");
            }
            return sb.ToString();
        }

        static MemoryStream ConvertStringToMemoryStream(string inputString)
        {
            // Convert string to byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(inputString);

            // Create MemoryStream from the byte array
            MemoryStream memoryStream = new MemoryStream(byteArray);

            return memoryStream;
        }

        private async Task SendAsync(MemoryStream responseStream)
        {
            _requestStream.Position = 0;
            responseStream.Position = 0;
            var response = await _client.SendAsync(_request, _requestStream, responseStream);
            await response.Content.CopyToAsync(_outBuffer);
        }

        [Benchmark]
        public async Task SendAsyncBodyMinimal() => await SendAsync(_responseStreamBodyMinimal);

        [Benchmark]
        public async Task SendAsyncBodySmall() => await SendAsync(_responseStreamBodySmall);

        [Benchmark]
        public async Task SendAsyncBodyLarge() => await SendAsync(_responseStreamBodyLarge);

        [Benchmark]
        public async Task SendAsyncHeadersBase() => await SendAsync(_responseStreamHeadersBase);

        [Benchmark]
        public async Task SendAsyncHeadersPlus40() => await SendAsync(_responseStreamHeadersPlus40);

        [Benchmark]
        public async Task SendAsyncHeadersPlus80() => await SendAsync(_responseStreamHeadersPlus80);

        [Benchmark]
        public async Task SendAsyncHeadersPlus120() => await SendAsync(_responseStreamHeadersPlus120);
    }
}
