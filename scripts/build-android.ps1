param(
    [string]$AndroidSdk = $env:ANDROID_HOME,
    [string]$JavaHome = $env:JAVA_HOME,
    [string]$Gradle = 'gradle',
    [switch]$Release
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!(Test-Path -LiteralPath (Join-Path $AndroidSdk 'platforms/android-36/android.jar'))) { throw '需要 Android SDK API 36 与 Build Tools 35.0.0。请通过 Android Studio 或 sdkmanager 安装。' }
if (!(Test-Path -LiteralPath (Join-Path $JavaHome 'bin/java.exe'))) { throw '请通过 -JavaHome 指定 JDK 17。' }
# A separate ASCII path avoids Android's Windows path restriction and OneDrive cache locks.
$taskBuild = Join-Path $env:TEMP ('PlanReminder-Android-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBuild | Out-Null
foreach ($name in @('app','build.gradle','settings.gradle','gradle.properties')) {
    if ($name -eq 'app') {
        New-Item -ItemType Directory -Path "$taskBuild/app" | Out-Null
        Copy-Item -LiteralPath "$repo/android/app/src" -Destination "$taskBuild/app/src" -Recurse
        Copy-Item -LiteralPath "$repo/android/app/build.gradle" -Destination "$taskBuild/app/build.gradle"
    } else { Copy-Item -LiteralPath "$repo/android/$name" -Destination "$taskBuild/$name" }
}
$env:JAVA_HOME = $JavaHome
$env:ANDROID_HOME = $AndroidSdk
$env:GRADLE_USER_HOME = Join-Path $env:TEMP 'PlanReminder-Gradle'
$env:PATH = "$JavaHome/bin;$env:PATH"
$tasks = @('assembleDebug','assembleDebugAndroidTest','lintDebug')
try {
    if ($Release) {
        $signing = Join-Path $repo '.signing'
        New-Item -ItemType Directory -Path $signing -Force | Out-Null
        $key = Join-Path $signing 'plan-reminder-release.jks'
        $config = Join-Path $signing 'credentials.json'
        if (!(Test-Path -LiteralPath $key)) {
            if (Test-Path -LiteralPath $config) { throw '密钥缺失，不能另建密钥替代。请恢复原签名密钥。' }
            $secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            [IO.File]::WriteAllText($config,(@{password=$secret} | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
            $env:PLAN_STORE_PASSWORD = $secret
            & "$JavaHome/bin/keytool.exe" -genkeypair -keystore $key -storetype JKS -storepass:env PLAN_STORE_PASSWORD -keypass:env PLAN_STORE_PASSWORD -alias planreminder -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Plan Reminder, O=flyfishliu031-lab, C=CN'
            if ($LASTEXITCODE -ne 0) { throw '签名密钥生成失败。请保留 .signing 目录以便排查。' }
        } else {
            if (!(Test-Path -LiteralPath $config)) { throw '签名密码配置缺失，请恢复 .signing/credentials.json。' }
            $env:PLAN_STORE_PASSWORD = (Get-Content -LiteralPath $config -Raw | ConvertFrom-Json).password
        }
        $env:PLAN_KEYSTORE = $key
        $tasks += 'assembleRelease'
    }
    & $Gradle -p $taskBuild --no-daemon --max-workers=2 @tasks
    if ($LASTEXITCODE -ne 0) { throw "安卓检查失败，诊断文件位于 $taskBuild。" }
    if ($Release) {
        $out = Join-Path $repo 'artifacts/PlanReminder-1.5.0-Android.apk'
        & "$AndroidSdk/build-tools/35.0.0/apksigner.bat" verify --verbose "$taskBuild/app/build/outputs/apk/release/app-release.apk"
        if ($LASTEXITCODE -ne 0) { throw 'APK 签名检查失败。' }
        Copy-Item -LiteralPath "$taskBuild/app/build/outputs/apk/release/app-release.apk" -Destination $out -Force
        Write-Output "安卓安装包：$out"
        Write-Output '请私下备份 .signing 目录；后续更新必须使用同一个签名密钥。密钥与密码不能上传 GitHub。'
    }
    Write-Output "检查报告：$taskBuild/app/build/reports"
} finally {
    Remove-Item Env:PLAN_STORE_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:PLAN_KEYSTORE -ErrorAction SilentlyContinue
}
