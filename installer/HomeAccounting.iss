#define AppName "Домашний бюджет"
#define AppPublisher "Офис пенсионера"
#define AppURL "https://github.com/andrey1b/HomeAccounting"
#define AppExeName "HomeAccounting.exe"
; Версия берётся из build-системы через /DAppVersion=x.y.z
#ifndef AppVersion
  #define AppVersion "4.2.0"
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
; Нужны права администратора для правила брандмауэра
PrivilegesRequired=admin
SetupIconFile=..\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительные параметры:"; Flags: unchecked

[Files]
; Основной исполняемый файл (self-contained, все зависимости внутри)
Source: "..\publish\HomeAccounting_v{#AppVersion}.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";       Filename: "{app}\{#AppExeName}"
Name: "{group}\Удалить {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Открываем порт 8772 в брандмауэре Windows
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall add rule name=""HomeAccounting порт 8772"" dir=in action=allow protocol=TCP localport=8772 profile=private"; \
    Flags: runhidden; StatusMsg: "Открываем порт 8772 для приёма чеков..."

; Запустить программу после установки
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Удаляем правило брандмауэра при деинсталляции
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall delete rule name=""HomeAccounting порт 8772"""; \
    Flags: runhidden
