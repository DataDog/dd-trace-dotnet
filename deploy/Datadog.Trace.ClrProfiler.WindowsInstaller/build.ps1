$args = @(
    "Product.wxs",
    "WixUI_InstallDir_Custom.wxs",
    "-out", "output\",
    "-dPlatform=x64",
    "-dProductVersion=0.2.0",
    "-dConfiguration=Release",
    "-ext", "WixUIExtension"
)

Start-Process -NoNewWindow -Wait -FilePath "${Env:ProgramFiles(x86)}\WiX Toolset v3.14\bin\candle.exe" -ArgumentList $args

$args = @(
    ".\output\Product.wixobj",
    ".\output\WixUI_InstallDir_Custom.wixobj",
    "-out", "output\dd-trace-csharp.msi",
    "-ext", "WixUIExtension"
)

Start-Process -NoNewWindow -Wait -FilePath "${Env:ProgramFiles(x86)}\WiX Toolset v3.14\bin\light.exe" -ArgumentList $args