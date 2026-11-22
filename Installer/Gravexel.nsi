; GRAVEXEL - Windows installer (NSIS 3)
; Build:  makensis -DSRC=<folder with Gravexel.exe> -DOUT=<setup.exe> Gravexel.nsi
Unicode true
!include "MUI2.nsh"

!define APP_NAME "GRAVEXEL"
!define APP_ID "Gravexel"
!define APP_EXE "Gravexel.exe"
!ifndef VERSION
  !define VERSION "1.1.0"
!endif
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
!ifndef SRC
  !define SRC "..\Build"
!endif
!ifndef OUT
  !define OUT "Gravexel-Setup-${VERSION}.exe"
!endif

Name "${APP_NAME}"
OutFile "${OUT}"
InstallDir "$LOCALAPPDATA\Programs\${APP_ID}"
InstallDirRegKey HKCU "Software\${APP_ID}" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APP_NAME} ${VERSION}"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} installer"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "${APP_NAME}"

!define MUI_ICON "${SRC}\obj\icon.ico"
!define MUI_UNICON "${SRC}\obj\icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Play GRAVEXEL now"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Arabic"

Function .onInit
  ; .NET Framework 4.7.2 or newer (built into Windows 10 1803+ and Windows 11)
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 461808 ok 0 ok
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "GRAVEXEL needs Microsoft .NET Framework 4.8.$\n$\nOpen the download page now?" IDNO ok
    ExecShell "open" "https://dotnet.microsoft.com/download/dotnet-framework/net48"
  ok:
FunctionEnd

Section "Install"
  ; close a running copy so files can be replaced (give the game a moment to exit on its own first)
  Sleep 800
  nsExec::Exec 'taskkill /F /IM ${APP_EXE}'
  ; remove the earlier "GRAVITY SWITCH" install (progress is kept and migrated automatically)
  ReadRegStr $1 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GravitySwitch" "UninstallString"
  StrCmp $1 "" noold
    nsExec::Exec 'taskkill /F /IM GravitySwitch.exe'
    ExecWait '$1 /S'
  noold:
  SetOutPath "$INSTDIR"
  File "${SRC}\${APP_EXE}"
  File "${SRC}\${APP_EXE}.config"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "Software\${APP_ID}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Self-test (plays every level).lnk" "$INSTDIR\${APP_EXE}" "--selftest"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  ; silent install = in-game update: start the new version right away
  IfSilent 0 +2
    Exec '"$INSTDIR\${APP_EXE}"'
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM ${APP_EXE}'
  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"
  IfSilent keep
  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your saved progress?" IDNO keep
    RMDir /r "$LOCALAPPDATA\Gravexel"
  keep:
SectionEnd
