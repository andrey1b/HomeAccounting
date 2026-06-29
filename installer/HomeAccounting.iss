#define AppName "Домашний бюджет"
#define AppPublisher "Офис пенсионера"
#define AppURL "https://github.com/andrey1b/HomeAccounting"
#define AppExeName "HomeAccounting.exe"
; Версия берётся из build-системы через /DAppVersion=x.y.z
#ifndef AppVersion
  #define AppVersion "4.4.0"
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
SetupIconFile=..\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
russian.PreparingDotNet=Установка Microsoft .NET 8 (один раз, занимает 1-2 минуты)...

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительные параметры:"; Flags: unchecked

[Files]
Source: "..\publish\HomeAccounting_v{#AppVersion}.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion
Source: "..\publish\e_sqlite3.dll";                     DestDir: "{app}"; Flags: ignoreversion

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
function IsDotNet8Installed(): Boolean;
var
  KeyPath: String;
  Names:   TArrayOfString;
  I:       Integer;
  FindRec: TFindRec;
begin
  Result  := False;
  // Реестр (Inno Setup 32-bit автоматически читает WOW6432Node)
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetValueNames(HKLM, KeyPath, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
  // Запасная проверка по файловой системе
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

// Вызывается перед установкой — скачивает и ставит .NET 8 автоматически
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  TmpFile, Params: String;
  ErrCode: Integer;
begin
  Result := '';
  if IsDotNet8Installed() then Exit;

  WizardForm.PreparingLabel.Caption := CustomMessage('PreparingDotNet');

  TmpFile := ExpandConstant('{tmp}\dotnet8desktop.exe');
  Params  :=
    '-NoProfile -Command "' +
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest -Uri ' +
    '''https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe'' ' +
    '-OutFile ''' + TmpFile + '''"';

  Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ErrCode);

  if FileExists(TmpFile) then
    Exec(TmpFile, '/quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ErrCode)
  else
    Result := 'Не удалось загрузить Microsoft .NET 8.' + #13#10 +
              'Проверьте подключение к интернету и повторите установку.';
end;
