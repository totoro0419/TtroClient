Unicode True
!include "MUI2.nsh"
!include "FileFunc.nsh"
!ifndef VERSION
 !define VERSION "0.3.0-alpha.1"
!endif
!ifndef PAYLOAD
 !define PAYLOAD "../dist/portable"
!endif
!ifndef OUTPUT
 !define OUTPUT "../dist/TtroClient-Setup.exe"
!endif
Name "Ttro Client ${VERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\TtroClient\${VERSION}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\TtroClient.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "../LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Var Args
Var Restart
Section "Ttro Client" SEC_CLIENT
 SetOutPath "$INSTDIR"
 File /r "${PAYLOAD}/*"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\Ttro Client"
 CreateShortcut "$SMPROGRAMS\Ttro Client\Ttro Client.lnk" "$INSTDIR\TtroClient.exe"
 CreateShortcut "$SMPROGRAMS\Ttro Client\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient" "DisplayName" "Ttro Client ${VERSION}"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient" "DisplayVersion" "${VERSION}"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient" "UninstallString" '$"$INSTDIR\Uninstall.exe$"'
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient" "NoRepair" 1
SectionEnd
Function .onInstSuccess
 ${GetParameters} $Args
 ${GetOptions} $Args "/RESTART" $Restart
 IfErrors done
 Exec '$"$INSTDIR\TtroClient.exe$"'
 done:
FunctionEnd
Section "Uninstall"
 ; Worlds, accounts and profiles live outside this versioned application directory.
 RMDir /r "$INSTDIR"
 Delete "$SMPROGRAMS\Ttro Client\Ttro Client.lnk"
 Delete "$SMPROGRAMS\Ttro Client\Uninstall.lnk"
 RMDir "$SMPROGRAMS\Ttro Client"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TtroClient"
SectionEnd
