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
; Windows 10 2004+ (the app's minimum) always includes .NET Framework 4.8, so no runtime is installed
MinVersion=10.0.19041

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Bluetooth Battery Indicator"; Filename: "{app}\BluetoothBatteryIndicator.exe"
Name: "{group}\Uninstall BluetoothBatteryIndicator"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BluetoothBatteryIndicator"; ValueData: """{app}\BluetoothBatteryIndicator.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BluetoothBatteryIndicator.exe"; Description: "Launch Bluetooth Battery Indicator"; Flags: nowait postinstall skipifsilent

