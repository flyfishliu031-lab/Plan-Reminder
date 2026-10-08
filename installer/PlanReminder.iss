#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
[Setup]
AppId={{575393DA-E197-4747-A9D8-45E949FA0FD2}
AppName=计划表
AppVersion={#AppVersion}
AppPublisher=flyfishliu031-lab
AppPublisherURL=https://github.com/flyfishliu031-lab/Plan-Reminder
AppSupportURL=https://github.com/flyfishliu031-lab/Plan-Reminder/issues
DefaultDirName={localappdata}\Programs\PlanReminder
DefaultGroupName=计划表
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=PlanReminder-{#AppVersion}-Setup-x64
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\PlanReminder.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoDescription=计划表安装程序
[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\PlanReminder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\editor.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\assets\main.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\assets\settings.png"; DestDir: "{app}\assets"; Flags: ignoreversion
[Icons]
Name: "{group}\计划表"; Filename: "{app}\PlanReminder.exe"
Name: "{autodesktop}\计划表"; Filename: "{app}\PlanReminder.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\PlanReminder.exe"; Description: "打开计划表"; Flags: nowait postinstall skipifsilent
; User data lives outside {app}; uninstall deliberately preserves plans and backups.
