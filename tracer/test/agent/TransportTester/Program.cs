// See https://aka.ms/new-console-template for more information

using Datadog.Trace;

var shutdown = false;

static void Menu()
{
    Console.WriteLine("OPTIONS");
    Console.WriteLine("  Q - Exit");
    Console.WriteLine("  H - Help");
    Console.WriteLine("  Any Key - Send Trace");
}

Menu();

while (!shutdown)
{
    var entry = Console.ReadKey();
    Console.WriteLine();
    var keyChar = entry.KeyChar.ToString().ToUpperInvariant();
    if (keyChar == "Q")
    {
        return;
    }
    else if (keyChar == "H")
    {
        Menu();
    }
    else
    {
        using (var span = Tracer.Instance.StartActive("manual-span"))
        {
            Console.WriteLine("Span created");
        }
    }
}
