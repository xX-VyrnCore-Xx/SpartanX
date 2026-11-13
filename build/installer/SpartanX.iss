; SpartanX installer (Inno Setup 6.3+).
; Built by .github/workflows/release.yml, e.g.:
;   ISCC.exe /DAppVersion=0.1.2 /DArch=x64 /DSourceDir=..\..\out\win-x64 build\installer\SpartanX.iss
;
; Per-user install (no admin prompt) into %LOCALAPPDATA%\Programs\SpartanX, registered in
; Windows "Installed apps" with an uninstaller that closes every SpartanX process first.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion AppVersion
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\out\win-" + Arch
#endif
#if Arch == "arm64"
  #define ArchAllowed "arm64"
#else
  #define ArchAllowed "x64compatible"
#endif

#define AppName "SpartanX"
#define AppExe "SpartanX.exe"
#define RepoUrl "https://github.com/xX-VyrnCore-Xx/SpartanX"

[Setup]
; Never change AppId: it is how Windows recognises upgrades of the same app.
AppId={{8F3C2A51-6B7E-4C1D-9A2F-5E0B7D4C3A19}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=SpartanX contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#NumericVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
MinVersion=10.0.22621
OutputDir=..\..\dist
OutputBaseFilename=SpartanX-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\..\src\SpartanX.App\Assets\SpartanX.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\..\LICENSE
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; SpartanX closes itself (see PrepareToInstall); don't let Restart Manager touch Explorer.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[CustomMessages]
english.DeleteSettings=Also delete your SpartanX settings?
italian.DeleteSettings=Eliminare anche le impostazioni di SpartanX?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Leftovers from the ZIP builds (v0.1.0-0.1.1).
Type: files; Name: "{app}\Install.cmd"
Type: files; Name: "{app}\Uninstall.cmd"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Silent installs are in-app updates: bring SpartanX back afterwards.
Filename: "{app}\{#AppExe}"; Parameters: "--updated"; Flags: nowait; Check: WizardSilent

[UninstallRun]
; Close every SpartanX process, restore the taskbar, remove launch-at-login.
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall --silent"; Flags: waituntilterminated runhidden; RunOnceId: "SpartanXCleanup"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CloseSpartanX(const Exe: String);
var
  ResultCode: Integer;
begin
  if FileExists(Exe) then
    Exec(Exe, '--quit --silent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Graceful first (restores the taskbar and releases the menu bar / Dock space)...
  CloseSpartanX(ExpandConstant('{app}\{#AppExe}'));
  // ...then make sure nothing (e.g. a portable copy) still locks files.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('DeleteSettings'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\{#AppName}'), True, True, True);
end;
