IF ["%programfiles(x86)%"]==[""] SET ilmergePath="%programfiles%\Microsoft\ILMerge"
IF NOT ["%programfiles(x86)%"]==[""] SET ilmergePath="%programfiles(x86)%\Microsoft\ILMerge"
set binaryPath=%CD%\..\bin\Release
set outputPath=%CD%\..\bin\ILMerge
IF NOT EXIST "%outputPath%" MKDIR "%outputPath%"
%ilmergePath%\ilmerge /ndebug /target:winexe /out:"%outputPath%\ISCSIConsole.exe" "%binaryPath%\ISCSIConsole.exe" "%binaryPath%\Utilities.dll" "%binaryPath%\DiskAccessLibrary.FileSystems.Abstractions.dll" "%binaryPath%\DiskAccessLibrary.Win32.dll" "%binaryPath%\DiskAccessLibrary.dll" "%binaryPath%\ISCSI.dll"