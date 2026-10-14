[Setup]
AppName=Bluetooth Battery Indicator
AppVersion={#GetEnv('APP_VERSION')}
AppPublisher=zonnedev
AppPublisherURL=https://github.com/zonnedev/bluetooth-battery-indicator
DefaultDirName={autopf}\BluetoothBatteryIndicator
DefaultGroupName=BluetoothBatteryIndicator
OutputDir=..\artifacts
OutputBaseFilename=BluetoothBatteryIndicator_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\BluetoothBatteryIndicator.exe
SetupIconFile=..\BluetoothBatteryIndicator\Resources\app.ico
WizardStyle=modern
CloseApplications=force
ArchitecturesAllowed=x64compatible

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Bluetooth Battery Indicator"; Filename: "{app}\BluetoothBatteryIndicator.exe"
Name: "{group}\Uninstall BluetoothBatteryIndicator"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BluetoothBatteryIndicator"; ValueData: """{app}\BluetoothBatteryIndicator.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BluetoothBatteryIndicator.exe"; Description: "Launch Bluetooth Battery Indicator"; Flags: nowait postinstall skipifsilent

[Code]
// The app is published framework-dependent (a few MB instead of ~50 MB), so it needs the
// shared .NET 8 Desktop Runtime. Download and install it when it is missing; this also
// runs during the silent auto-update install.
const
  DotNetUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  DotNetFile = 'windowsdesktop-runtime-win-x64.exe';

function IsDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec) then
  begin
    try
      repeat
        if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax > 0 then
    WizardForm.PreparingLabel.Caption := Format('Downloading .NET Desktop Runtime 8... %d%%', [Progress * 100 div ProgressMax]);
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsDesktopRuntimeInstalled then
    Exit;

  WizardForm.PreparingLabel.Visible := True;
  try
    DownloadTemporaryFile(DotNetUrl, DotNetFile, '', @OnDownloadProgress);
  except
    Result := '.NET Desktop Runtime 8 download failed: ' + GetExceptionMessage + #13#10 +
      'Install it manually from https://dotnet.microsoft.com/download/dotnet/8.0 and run Setup again.';
    Exit;
  end;

  WizardForm.PreparingLabel.Caption := 'Installing .NET Desktop Runtime 8...';
  // The runtime installs machine-wide, so it needs elevation even though this setup does not
  ShellExec('runas', ExpandConstant('{tmp}\' + DotNetFile), '/install /quiet /norestart', '',
    SW_SHOW, ewWaitUntilTerminated, ResultCode);

  if not IsDesktopRuntimeInstalled then
    Result := '.NET Desktop Runtime 8 was not installed.' + #13#10 +
      'Install it manually from https://dotnet.microsoft.com/download/dotnet/8.0 and run Setup again.';
end;
