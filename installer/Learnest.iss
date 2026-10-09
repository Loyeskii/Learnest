; ============================================================================
;  Learnest - Inno Setup 6 script                        installer\Learnest.iss
;  Learnest 1.0.0 - "A gateway to everyday management"
;  A timeline & project manager for university students (WPF, Windows x64).
; ----------------------------------------------------------------------------
;  WHAT THIS SCRIPT PRODUCES
;      ..\dist\installer\Learnest-1.0.0-Setup.exe
;  It packs the whole *contents of ..\dist* - Learnest.exe, an optional
;  portable sqlite3.dll, an optional Lang\ folder with extra JSON language
;  packs, and whatever runtime files the build produced - into a normal
;  Windows installer with an uninstaller, shortcuts and a launch option.
;
;  IMPORTANT - the sources are saved as UTF-8 *with* a BOM because this file
;  contains Chinese text.  Keep the BOM when you edit it, otherwise Inno Setup
;  will mis-read the Chinese strings.
;
; ============================================================================
;  Inno Setup is NOT bundled with this repository - install it yourself
; ============================================================================
;  This repository ships no compiler and no binaries; the .iss file is only a
;  recipe.  Get Inno Setup 6 yourself (it is free):
;
;      winget install --id JRSoftware.InnoSetup -e
;
;  or download the installer from the official page:
;
;      https://jrsoftware.org/isdl.php
;
;  Version requirement: Inno Setup 6.3 or newer.  The directive
;  ArchitecturesInstallIn64BitMode=x64compatible below was introduced in 6.3.
;  ChineseSimplified.isl became an *official* bundled translation in 6.5.0;
;  the [Languages] section detects it at compile time, so with an older 6.x
;  compiler you simply get an English-only setup - the script still compiles.
;
;  EXACT COMMAND LINE TO BUILD SILENTLY (note the /Q for silent):
;
;      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /Q "C:\Users\TNTS\Documents\Files\Deepseek_harness\Todos\Learnest\installer\Learnest.iss"
;
;  Same thing with progress output (/Qp) and with the path made portable:
;
;      "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" /Qp Learnest.iss
;
;  Or use the wrapper script, which locates ISCC.exe for you, rebuilds dist
;  first and reports the finished installer:
;
;      pwsh -File installer\Build-Installer.ps1
;
; ============================================================================
;  WHERE THE USER PICKS THE INSTALL FOLDER           <<<<  READ THIS  >>>>
; ============================================================================
;  DisableDirPage=no below is MANDATORY and must stay "no": it keeps the
;  "Select Destination Location" wizard page visible so that the person
;  installing Learnest can always choose the folder - either by typing a path
;  into the edit box or by pressing "Browse..." to pick one from a tree.
;  Do NOT change it to "yes" and do NOT use "DisableDirPage=auto"; that would
;  remove the page and force our suggestion on the user.
;
;  DefaultDirName={autopf}\Learnest is only the *suggested* value shown on that
;  page.  {autopf} follows the install mode the user picked:
;      * per-machine  ("install for all users", needs elevation)
;            -> C:\Program Files\Learnest
;      * per-user     ("install for me only", no elevation)
;            -> %LocalAppData%\Programs\Learnest
;
;  UsePreviousAppDir=yes makes an upgrade suggest the folder of the previous
;  Learnest installation (still fully editable on the directory page).
;
;  PrivilegesRequiredOverridesAllowed=dialog shows a dialog before the wizard
;  in which the user chooses between the two install modes above; the command
;  line switches /CURRENTUSER and /ALLUSERS are accepted as well.
;
;  The user never picks where the *data* goes at install time: Learnest stores
;  no business data at all.  On first launch the program itself asks the user
;  to CREATE a new container folder or OPEN an existing one.
;
; ============================================================================
;  DATA SAFETY
;  The installer writes only into {app}, the Start Menu, the desktop and the
;  current user's Startup folder.  It never creates or touches a container,
;  and the generated uninstaller removes only what it installed - the
;  container folder and %LocalAppData%\Learnest\appsettings.json are left
;  untouched, so uninstalling never destroys a student's todo list.
; ============================================================================

#define AppName "Learnest"
#define AppVersion "1.0.0"
#define AppPublisher "Loyeskii"
#define AppTagline "A gateway to everyday management"
#define AppExeName "Learnest.exe"

; Path of the optional Simplified Chinese messages file inside the compiler's
; own installation (this is how Inno Setup 6.5+ ships it).  Its existence is
; tested below so that older compilers still build successfully.
#define ChineseIslFile AddBackslash(CompilerPath) + "Languages\ChineseSimplified.isl"

[Setup]
; A stable AppId: never change it, or Windows will treat a new version as a
; different product and upgrades will not replace the old installation.
AppId={{D9894CE8-93E8-4A49-AC2B-54924A4FFDF6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppComments={#AppTagline}
VersionInfoVersion=1.0.0.0
; The installer executable carries the same icon as the application.
SetupIconFile=..\assets\learnest.ico
DefaultDirName={autopf}\Learnest
DefaultGroupName={#AppName}
; ---------------------------------------------------------------------------
; MANDATORY: the directory page is shown so the user can choose the install
; folder.  See the header comment block.  Keep this at "no".
DisableDirPage=no
; ---------------------------------------------------------------------------
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist\installer
OutputBaseFilename=Learnest-1.0.0-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AllowNoIcons=yes
CloseApplications=yes
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\Learnest.exe

[Languages]
; English is always available (Default.isl ships with every Inno Setup 6).
Name: "english"; MessagesFile: "compiler:Default.isl"
; Simplified Chinese, only when this Inno Setup installation actually provides
; the (official since 6.5.0) ChineseSimplified.isl.  Nothing to configure: on
; an older compiler the two lines are skipped and the setup stays English.
#if FileExists(ChineseIslFile)
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif

[CustomMessages]
; Task labels for the "start with Windows" checkbox ("desktopicon" reuses the
; English strings that ship inside Inno Setup, so they need no override here).
english.StartupTask=Start Learnest automatically when Windows starts (adds a shortcut to your Startup folder)
#if FileExists(ChineseIslFile)
chinesesimplified.StartupTask=Windows 启动时自动运行 Learnest（在“启动”文件夹中创建快捷方式）
#endif

[Tasks]
; Optional desktop icon.  {autodesktop} resolves to the shared desktop for a
; per-machine install and to the current user's desktop for a per-user install.
; If you would rather always write to the shared desktop, replace {autodesktop}
; in the [Icons] section with {commondesktop} (requires the all-users mode).
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Optional "auto start Learnest with Windows" - a shortcut in the Startup folder.
Name: "startupicon"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The entire built program: Learnest.exe, an optional portable sqlite3.dll, an
; optional Lang\*.json with extra language packs, and any runtime files the
; selected toolchain produced (see docs\BUILD.md).  subdirectories such as
; zh-Hans\ or Lang\ are recreated inside {app}.
; Excluded: debug symbols (*.pdb) and any container export archive, which is
; user data named "yyyy-MM-dd_HH-mm-ss.containername.learnest.zip" - never ship
; somebody's personal container inside the installer.
Source: "..\dist\*"; DestDir: "{app}"; Excludes: "*.pdb,*.learnest.zip"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu entry (per-machine -> the shared Start Menu, per-user -> the
; user's own Start Menu, because {autoprograms} follows the install mode).
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "{#AppTagline}"
; Optional desktop icon, see the [Tasks] comment about {commondesktop}.
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "{#AppTagline}"; Tasks: desktopicon
; Optional auto start with Windows.  {userstartup} is the Startup folder of the
; user running the setup; for an all-users auto start use {commonstartup}
; instead (that only works in the per-machine mode).
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "{#AppTagline}"; Tasks: startupicon

[Run]
; Optional: launch Learnest right after a successful install.  Learnest then
; shows its first-run wizard, which asks for a new or an existing container.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; ----------------------------------------------------------------------------
; The uninstaller is generated automatically by Inno Setup (unins000.exe) and
; is registered in "Apps & features" with UninstallDisplayIcon above.
; No [UninstallDelete] entry is needed or wanted: the container folder (the
; user's data) and %LocalAppData%\Learnest\appsettings.json deliberately
; survive an uninstall.
; ----------------------------------------------------------------------------
