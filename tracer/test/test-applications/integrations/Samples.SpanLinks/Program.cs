// See https://aka.ms/new-console-template for more information

using Samples;

Console.WriteLine("Hello, World!");
for (var i = 0; i < 100; i++)
{
    IDisposable? root;
    root = SampleHelpers.CreateScope("root");
    Console.WriteLine("Started root");

    var link = SampleHelpers.CreateScope("link");
    Console.WriteLine("link");
    var attributesToAdd = new List<KeyValuePair<string, string>> { new("link.name", "manually_linking"), new("pair", "false"), new("arbitrary", "56709") };


    var result = SampleHelpers.AddSpanLinkWithAttributes(link, root, attributesToAdd);

    if (result is not null)
    {
        Console.WriteLine("added link");
    }
    

    link.Dispose();
    root.Dispose();
}
