$args = @(
    "Product.wxs",
    "WixUI_InstallDir_Custom.wxs",
    "-out", "output\",
    "-arch", "x64",
    "-dPlatform=x64",
    "-dConfiguration=Release",
    "-ext", "WixUIExtension"
)

Start-Process -NoNewWindow -Wait -FilePath "${Env:ProgramFiles(x86)}\WiX Toolset v3.11\bin\candle.exe" -ArgumentList $args

$args = @(
    ".\output\Product.wixobj",
    ".\output\WixUI_InstallDir_Custom.wixobj",
    "-out", "output\dd-trace-csharp.msi",
    "-ext", "WixUIExtension"
)

Start-Process -NoNewWindow -Wait -FilePath "${Env:ProgramFiles(x86)}\WiX Toolset v3.11\bin\light.exe" -ArgumentList $args