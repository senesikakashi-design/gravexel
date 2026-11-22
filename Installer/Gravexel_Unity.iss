; Inno Setup script for the Unity build of GRAVEXEL.
; Used by the Unity menu "GRAVEXEL > 3. Build Windows Installer".
#define AppName "GRAVEXEL"
#define AppVersion "1.0.0"
#define BuildDir "..\Unity\Builds\Windows"

[Setup]
AppId={{6C0E7B1A-2F4D-4E7B-9A51-5A7D3C9E2B11}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Gravexel
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=..\Unity\Builds\Installer
OutputBaseFilename=Gravexel-Unity-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Gravexel.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "*_BurstDebugInformation_DoNotShip\*"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Gravexel.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Gravexel.exe"

[Run]
Filename: "{app}\Gravexel.exe"; Description: "Play {#AppName}"; Flags: nowait postinstall skipifsilent
