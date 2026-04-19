; SSMS Quick Restore - Inno Setup script
;
; Builds an installer that:
;   1. Asks the user where SSMS is installed (auto-detects 21 / 22).
;   2. Copies the extension files to <SSMS>\Common7\IDE\Extensions\SsmsQuickRestore\.
;   3. Patches the pkgdef so SSMS resolves the DLL via $PackageFolder$.
;   4. Runs Ssms.exe /updateconfiguration so SSMS picks up the new package.
;
; Build:  & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\SsmsQuickRestore.iss
;
; Output: installer\Output\SsmsQuickRestore-Setup-<version>.exe

#define AppName       "SSMS Quick Restore"
#define AppVersion    "1.0.0"
#define AppPublisher  "anzdev4life"
#define AppURL        "https://github.com/annguyen209/SsmsQuickRestore"
#define BuildOutput   "..\src\bin\Release\net48"

[Setup]
AppId={{C8F1A2B3-D4E5-4A6B-9C7D-8E9F0A1B2C3D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
UninstallDisplayName={#AppName} {#AppVersion}
DefaultDirName={code:GetSsmsExtensionsDir}
DisableDirPage=no
DirExistsWarning=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=SsmsQuickRestore-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=force
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; Note: SsmsRestoreDrop.pkgdef must already be patched (Assembly -> CodeBase) before
; running ISCC. Use installer\build-installer.ps1 which handles the patch step.
[Files]
Source: "{#BuildOutput}\SsmsRestoreDrop.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\SsmsRestoreDrop.pkgdef";    DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\extension.vsixmanifest";    DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\Resources\*";               DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs

[Run]
Filename: "{code:GetSsmsExePath}"; Parameters: "/updateconfiguration"; \
    StatusMsg: "Refreshing SSMS configuration..."; Flags: runhidden

[UninstallRun]
Filename: "{code:GetSsmsExePath}"; Parameters: "/updateconfiguration"; \
    RunOnceId: "SsmsUpdateConfigUninstall"; Flags: runhidden

[Code]
const
  SSMS22_DEFAULT  = 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE';
  SSMS21_DEFAULT  = 'C:\Program Files\Microsoft SQL Server Management Studio 21\Release\Common7\IDE';
  SSMS_DOWNLOAD   = 'https://aka.ms/ssms/22/release/install';

function FindSsmsViaRegistry(): String;
var
  Root: String;
begin
  Result := '';
  // Microsoft Visual Studio installer writes SSMS install info under the VisualStudio_Setup hive.
  // The InstallationPath value points to the install root; the IDE folder is Common7\IDE under it.
  if RegQueryStringValue(HKLM,
       'SOFTWARE\Microsoft\VisualStudio\Setup\SSMS\22',
       'InstallationPath', Root) then
  begin
    Result := AddBackslash(Root) + 'Common7\IDE';
    if DirExists(Result) then Exit;
  end;
  if RegQueryStringValue(HKLM,
       'SOFTWARE\Microsoft\VisualStudio\Setup\SSMS\21',
       'InstallationPath', Root) then
  begin
    Result := AddBackslash(Root) + 'Common7\IDE';
    if DirExists(Result) then Exit;
  end;
  Result := '';
end;

function FindSsmsIdeDir(): String;
begin
  if DirExists(SSMS22_DEFAULT) then
    Result := SSMS22_DEFAULT
  else if DirExists(SSMS21_DEFAULT) then
    Result := SSMS21_DEFAULT
  else
  begin
    Result := FindSsmsViaRegistry();
    if Result = '' then Result := SSMS22_DEFAULT;  // placeholder
  end;
end;

function IsSsmsInstalled(): Boolean;
begin
  Result := DirExists(SSMS22_DEFAULT)
         or DirExists(SSMS21_DEFAULT)
         or (FindSsmsViaRegistry() <> '');
end;

function GetSsmsExtensionsDir(Param: String): String;
begin
  Result := FindSsmsIdeDir() + '\Extensions\SsmsQuickRestore';
end;

function GetSsmsExePath(Param: String): String;
var
  Dir: String;
begin
  // {app} = ...\IDE\Extensions\SsmsQuickRestore.
  // ExtractFileDir trims trailing backslash so two calls walk up two levels:
  //   step 1: ...\IDE\Extensions
  //   step 2: ...\IDE
  Dir := ExtractFileDir(ExtractFileDir(ExpandConstant('{app}')));
  Result := AddBackslash(Dir) + 'Ssms.exe';
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  if not IsSsmsInstalled() then
  begin
    if MsgBox('SQL Server Management Studio was not found.' + #13#10 + #13#10 +
              'This extension only works inside SSMS 21 or 22 - install SSMS first, ' +
              'then run this installer again.' + #13#10 + #13#10 +
              'Open the SSMS download page in your browser?',
              mbCriticalError, MB_YESNO) = IDYES then
    begin
      ShellExec('open', SSMS_DOWNLOAD, '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
    Exit;
  end;
  Result := True;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // Skip the "Select Destination Location" page when SSMS was auto-detected.
  // We never reach InstallSetup with an unknown path because IsSsmsInstalled()
  // blocks installation up front.
  if PageID = wpSelectDir then
    Result := IsSsmsInstalled()
  else
    Result := False;
end;
