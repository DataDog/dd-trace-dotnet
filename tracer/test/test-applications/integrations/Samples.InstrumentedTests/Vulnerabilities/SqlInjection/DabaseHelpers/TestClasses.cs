namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class Book
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }

    [DelegateDecompiler.Computed]
    public string FullId
    {
        get => Id + "-" + Title + "-" + Author;
    }

    public string FullTitle
    {
        [DelegateDecompiler.Computed]
        get => Title + "_";
    }

    public string FullAuthor
    {
        get => "_" + Author;
    }

}
