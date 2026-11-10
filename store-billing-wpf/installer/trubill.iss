; TruBill Windows installer.
; Compile from publish-store-billing-client.ps1:
;   ISCC /DSourceDir=... /DOutputDir=... trubill.iss
;
; First install copies .env from beside setup.exe when the install folder has none.
; Upgrades replace trubill.exe and leave the existing .env in place.

#ifndef SourceDir
  #define SourceDir "..\dist\trubill-win-x64-single"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist\trubill-win-x64-single"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{8F3C1A2E-6B47-4D91-9C0E-5A7B2D4F8E11}
AppName=TruBill
AppVersion={#AppVersion}
AppPublisher=RR Bridal
DefaultDirName={autopf}\TruBill
DefaultGroupName=TruBill
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=setup
SetupIconFile=..\src\RRBridal.StoreBilling.App\Resources\Assets\TruBill.ico
UninstallDisplayIcon={app}\trubill.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no
WizardStyle=modern

[Files]
Source: "{#SourceDir}\trubill.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\TruBill"; Filename: "{app}\trubill.exe"
Name: "{autodesktop}\TruBill"; Filename: "{app}\trubill.exe"

[Run]
Filename: "{app}\trubill.exe"; Description: "Launch TruBill"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  SrcEnv, DestEnv: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  DestEnv := ExpandConstant('{app}\.env');
  if FileExists(DestEnv) then
    Exit;

  SrcEnv := ExpandConstant('{src}\.env');
  if FileExists(SrcEnv) then
    FileCopy(SrcEnv, DestEnv, False);
end;
