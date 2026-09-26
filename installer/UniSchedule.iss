; Per-user setup. The database stays in %LocalAppData%\UniSchedule and is not installed here.

#define MyAppName "Расписание"
#define MyAppVersion "1.0.0"
#define MyAppExe "UniSchedule.exe"
#define MyAppPublisher "UniSchedule"

[Setup]
AppId={{8F4C2A1E-6B37-4D9A-9E15-2C7A0B8D4F61}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\UniSchedule
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=UniSchedule-Setup
SetupIconFile=..\UniSchedule\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\dist\UniSchedule\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RunValue: string;
  InstalledExe: string;
begin
  if CurUninstallStep <> usUninstall then
    Exit;
  if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'UniSchedule', RunValue) then
    Exit;
  InstalledExe := ExpandConstant('{app}\{#MyAppExe}');
  if CompareText(RemoveQuotes(RunValue), InstalledExe) = 0 then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'UniSchedule');
end;
