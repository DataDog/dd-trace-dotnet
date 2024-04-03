// See https://aka.ms/new-console-template for more information
using Samples;

Console.WriteLine("Hello, World!");
IDisposable? root;
using (root = SampleHelpers.CreateScope("root"))
{
    Console.WriteLine("Started root");
}

using (var link = SampleHelpers.CreateScope("link"))
{
    Console.WriteLine("link");
    var result = SampleHelpers.AddSpanLinkWithAttributes(root, link, null);

    if (result is not null) 
    {
        Console.WriteLine("added link");
    }
}
