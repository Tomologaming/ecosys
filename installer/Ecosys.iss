#define AppName "Ecosys"
#define AppVersion "0.1.0"
#define AppPublisher "Ecosys Open Source"
#define AppExeName "Ecosys.Windows.exe"
#define SafeAssetUrl "https://github.com/Tomologaming/ecosys/releases/latest/download/Ecosys-windows-safe.zip"

[Setup]
AppId={{B7E2D4A8-8C1B-4D0D-9B3C-ECOSYS000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Ecosys
DefaultGroupName=Ecosys
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=Ecosys-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupArchitecture=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Icons]
Name: "{group}\Ecosys"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Ecosys"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Ecosys starten"; Flags: nowait postinstall skipifsilent

[Code]
const
  SafeAssetUrl = '{#SafeAssetUrl}';

var
  DownloadPage: TDownloadWizardPage;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    SetupMessage(msgWizardPreparing),
    'Die aktuelle sichere Ecosys-Version wird heruntergeladen.',
    @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ArchivePath: String;
begin
  Result := True;

  if CurPageID <> wpReady then
    Exit;

  DownloadPage.Clear;
  DownloadPage.Add(SafeAssetUrl, 'Ecosys-windows-safe.zip', '');
  DownloadPage.Show;

  try
    DownloadPage.Download;
    ArchivePath := ExpandConstant('{tmp}\Ecosys-windows-safe.zip');
    ExtractArchive(ArchivePath, ExpandConstant('{app}'), '', True, nil);
  except
    MsgBox(
      'Die sichere Ecosys-Version konnte nicht heruntergeladen oder entpackt werden.'#13#10#13#10 +
      'Bitte prüfe deine Internetverbindung und versuche es erneut.',
      mbCriticalError, MB_OK);
    Result := False;
  finally
    DownloadPage.Hide;
  end;
end;
