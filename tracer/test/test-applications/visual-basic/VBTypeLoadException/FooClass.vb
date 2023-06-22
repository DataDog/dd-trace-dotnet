Imports Datadog.Trace

Public Class FooClass
    Public Shared Sub Foo()
        Dim activeScope = Tracer.Instance.StartActive("Foo")
        Console.WriteLine("From VBLibrary")

        activeScope.Close()
    End Sub
End Class

