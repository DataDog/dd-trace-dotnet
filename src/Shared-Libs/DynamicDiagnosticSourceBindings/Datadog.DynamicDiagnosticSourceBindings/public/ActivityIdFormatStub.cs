namespace OpenTelemetry.DynamicActivityBinding
{
    /// <summary>
    /// The possibilities for the format of the Activity ID.
    /// The values need to map exactly to
    /// https://github.com/dotnet/runtime/blob/db6c3205edb3ad2fd7bc2b4a77bbaadbf3c95945/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs#L1513
    /// 
    /// </summary>
    public enum ActivityIdFormatStub
    {
        Unknown = 0,       // ID format is not known.
        Hierarchical = 1,  //|XXXX.XX.X_X ... see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md#id-format
        W3C = 2,           // 00-XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX-XXXXXXXXXXXXXXXX-XX see https://w3c.github.io/trace-context/
    }
}