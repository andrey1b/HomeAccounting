#define AppName "Домашний бюджет"
#define AppPublisher "Офис пенсионера"
#define AppURL "https://github.com/andrey1b/HomeAccounting"
#define AppExeName "HomeAccounting.exe"
#define DotNetUrl "https://dotnet.microsoft.com/download/dotnet/8.0/runtime"
; Версия берётся из build-системы через /DAppVersion=x.y.z
#ifndef AppVersion
  #define AppVersion "4.2.1"
#endif

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\HomeAccounting
DefaultGroupName={#AppName}
OutputDir=..\installer-out
OutputBaseFilename=HomeAccounting_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительные параметры:"; Flags: unchecked

[Files]
Source: "..\publish\HomeAccounting_v{#AppVersion}.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Удалить {#AppName}";   Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall add rule name=""HomeAccounting порт 8772"" dir=in action=allow protocol=TCP localport=8772 profile=private"; \
    Flags: runhidden; StatusMsg: "Открываем порт 8772 для приёма чеков..."
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall delete rule name=""HomeAccounting порт 8772"""; \
    Flags: runhidden

[Code]
// Проверяем наличие .NET 8 Desktop Runtime
function IsDotNet8Installed(): Boolean;
var
  KeyPath: String;
  Names:   TArrayOfString;
  I:       Integer;
begin
  Result  := False;
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetValueNames(HKLM, KeyPath, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime не найден.' + #13#10 +
      'Программа требует его для работы.' + #13#10#13#10 +
      'Нажмите OK, чтобы открыть страницу загрузки Microsoft,' + #13#10 +
      'скачайте и установите runtime, затем запустите этот установщик снова.',
      mbInformation, MB_OKCANCEL) = IDOK then
      ShellExec('open', '{#DotNetUrl}', '', '', SW_SHOW, ewNoWait, Result);
    Result := False;
  end;
end;
