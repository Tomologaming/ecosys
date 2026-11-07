#define AppName "Ecosys"
#define AppVersion "0.1.0"
#define AppPublisher "Ecosys Open Source"
#define AppExeName "Ecosys.Windows.exe"
#define AppId "{{B7E2D4A8-8C1B-4D0D-9B3C-ECOSYS000001}"
#define SafeAssetUrl "https://github.com/Tomologaming/ecosys/releases/latest/download/Ecosys-windows-safe.zip"
#define SafeChecksumUrl "https://github.com/Tomologaming/ecosys/releases/latest/download/Ecosys-windows-safe.zip.sha256"

[Setup]
; Same AppId as earlier releases so existing installations upgrade instead of duplicating.
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
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
SetupIconFile=..\windows\Ecosys.Windows\Assets\ecosys.ico
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763
; The online installer unpacks the release ZIP at runtime, which needs the full extraction engine.
ArchiveExtraction=full

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
#ifdef OfflineMode
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
#endif

[Icons]
Name: "{group}\Ecosys"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Ecosys"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Ecosys starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The online installer writes the payload with Pascal code, so Inno Setup does not
; track those files. Remove the whole application directory on uninstall.
Type: filesandordirs; Name: "{app}"

[Code]
#ifdef OfflineMode
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
end;
#else
const
  SafeAssetUrl = '{#SafeAssetUrl}';
  ChecksumUrl = '{#SafeChecksumUrl}';
  ArchiveName = 'Ecosys-windows-safe.zip';

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

function IsSha256Hex(const Value: String): Boolean;
var
  Index: Integer;
  Character: Char;
begin
  Result := Length(Value) = 64;
  if not Result then
    Exit;
  for Index := 1 to Length(Value) do
  begin
    Character := Value[Index];
    if not (((Character >= '0') and (Character <= '9')) or
            ((Character >= 'a') and (Character <= 'f')) or
            ((Character >= 'A') and (Character <= 'F'))) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ArchivePath, ChecksumPath, ExpectedHash: String;
  ChecksumContents: AnsiString;
begin
  Result := True;
  if CurPageID <> wpReady then
    Exit;

  DownloadPage.Clear;
  DownloadPage.Add(ChecksumUrl, ArchiveName + '.sha256', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      ChecksumPath := ExpandConstant('{tmp}\' + ArchiveName + '.sha256');
      if not LoadStringFromFile(ChecksumPath, ChecksumContents) then
        RaiseException('Die Prüfsumme konnte nicht gelesen werden.');
      ExpectedHash := Trim(String(ChecksumContents));
      if not IsSha256Hex(ExpectedHash) then
        RaiseException('Die Prüfsumme hat ein unerwartetes Format.');

      DownloadPage.Clear;
      DownloadPage.Add(SafeAssetUrl, ArchiveName, ExpectedHash);
      DownloadPage.Download;

      ArchivePath := ExpandConstant('{tmp}\' + ArchiveName);
      ForceDirectories(ExpandConstant('{app}'));
      ExtractArchive(ArchivePath, ExpandConstant('{app}'), '', True, nil);

      if not FileExists(ExpandConstant('{app}\{#AppExeName}')) then
        RaiseException('Das Archiv enthielt die Ecosys-Anwendung nicht.');
    except
      MsgBox('Die sichere Ecosys-Version konnte nicht heruntergeladen oder entpackt werden.'#13#13 +
             'Bitte prüfe deine Internetverbindung und versuche es erneut.', mbCriticalError, MB_OK);
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;
end;
#endif
