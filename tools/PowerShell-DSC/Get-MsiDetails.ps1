param (
    [string]$path
)

# Get-WmiObject Win32_Product | Format-Table IdentifyingNumber, Name, LocalPackage
# Get-CimInstance -ClassName Win32_Product -Filter "Name like 'Datadog%'" | Format-Table IdentifyingNumber, Name, LocalPackage

$sig = @'
[DllImport("msi.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
private static extern UInt32 MsiOpenPackageW(string szPackagePath, out IntPtr hProduct);

[DllImport("msi.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
private static extern UInt32 MsiCloseHandle(IntPtr hAny);

[DllImport("msi.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
private static extern UInt32 MsiGetPropertyW(IntPtr hAny, string name, System.Text.StringBuilder buffer, ref int bufferLength);

public static IntPtr OpenPackage(string path)
{
    IntPtr msiHandle;
    var result = MsiOpenPackageW(path, out msiHandle);
    return result == 0 ? msiHandle : IntPtr.Zero;
}

public static void ClosePackage(IntPtr msiHandle)
{
    if (msiHandle != IntPtr.Zero)
    {
        MsiCloseHandle(msiHandle);
    }
}

public static string GetPackageProperty(IntPtr msiHandle, string property)
{
    int length = 256;
    var buffer = new System.Text.StringBuilder(length);
    var result = MsiGetPropertyW(msiHandle, property, buffer, ref length);
    return buffer.ToString();
}
'@

$properties = 'ProductName', 'ProductCode', 'ProductVersion'
$msiTools = Add-Type -PassThru -Name 'MsiTools' -MemberDefinition $sig
Write-Host "Opening msi package $path..."

try {
    $msiHandle = $msiTools::OpenPackage($path);

    if ($msiHandle -eq [IntPtr]::Zero) {
        Write-Host 'Error opening msi package.'
    }
    else {

        Write-Host "Opened msi package."

        foreach ($property in $properties) {
            $value = $msiTools::GetPackageProperty($msiHandle, $property)
            Write-Host "$property = $value"
        }
    }
}
finally {
    $msiTools::ClosePackage($msiHandle)
    Write-Host 'Closed msi package.'
}
