param([string]$InnoCompiler, [string]$Version = '1.5.0')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.build-cache\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
# WinForms does not require optional mobile/browser workloads.
$env:MSBuildEnableWorkloadResolver = 'false'
dotnet build -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw '构建失败。' }
dotnet '.\bin\Release\net10.0-windows\PlanReminder.dll' --self-test
if ($LASTEXITCODE -ne 0) { throw '功能检查失败。' }
dotnet '.\bin\Release\net10.0-windows\PlanReminder.dll' --ui-check artifacts\ui-preview
if ($LASTEXITCODE -ne 0) { throw '界面布局检查失败。' }
dotnet publish -c Release -r win-x64 --self-contained true -p:Version=$Version -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\publish
if ($LASTEXITCODE -ne 0) { throw '打包失败。' }
Copy-Item -LiteralPath README.md -Destination artifacts\publish\README.md -Force
New-Item -ItemType Directory -Path artifacts\publish\assets -Force | Out-Null
Copy-Item -LiteralPath assets\editor.png -Destination artifacts\publish\assets\editor.png -Force
Copy-Item -LiteralPath assets\main.png -Destination artifacts\publish\assets\main.png -Force
Copy-Item -LiteralPath assets\settings.png -Destination artifacts\publish\assets\settings.png -Force
Copy-Item -LiteralPath assets\long-term.png -Destination artifacts\publish\assets\long-term.png -Force
Copy-Item -LiteralPath assets\editor-long-term.png -Destination artifacts\publish\assets\editor-long-term.png -Force
Copy-Item -LiteralPath assets\editor-multiline.png -Destination artifacts\publish\assets\editor-multiline.png -Force
Copy-Item -LiteralPath assets\editor-alarm.png -Destination artifacts\publish\assets\editor-alarm.png -Force
Copy-Item -LiteralPath assets\alarm.png -Destination artifacts\publish\assets\alarm.png -Force
Compress-Archive -LiteralPath artifacts\publish\PlanReminder.exe,artifacts\publish\README.md,artifacts\publish\assets -DestinationPath "artifacts\PlanReminder-$Version-Portable-x64.zip" -Force
if (!$InnoCompiler) {
    $compilerCandidates = @('.build-tools\inno\ISCC.exe', 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 7\ISCC.exe')
    $InnoCompiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$InnoCompiler) { throw '便携版已生成。生成安装包还需要 Inno Setup 6.7+；通过 -InnoCompiler 指定 ISCC.exe。' }
& $InnoCompiler "/DAppVersion=$Version" installer\PlanReminder.iss
if ($LASTEXITCODE -ne 0) { throw '安装包生成失败。' }
$files = Get-ChildItem -LiteralPath artifacts -File | Where-Object { $_.Name -like "PlanReminder-$Version-*.exe" -or $_.Name -like "PlanReminder-$Version-*.zip" }
$lines = $files | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
$lines | Set-Content -LiteralPath artifacts\SHA256SUMS.txt -Encoding utf8
Write-Host '完成：安装包、便携版和校验文件均位于 artifacts 目录。'
