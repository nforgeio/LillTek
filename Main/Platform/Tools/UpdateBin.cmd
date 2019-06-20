@echo on
REM This batch file copies the DEBUG bootstrap tools to the %LT_TOOLBIN% folder
REM to make it easier to update these files after modifying a tool.

set LT_TOOLS=%LT_ROOT%\Platform\Tools

copy "%LT_TOOLS%\DBPackage\bin\Debug\DBPackage.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\InstallHelper\bin\Debug\InstallHelper.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\PathTool\bin\Debug\PathTool.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\Timestamp\bin\Debug\Timestamp.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\TSQLPP\bin\Debug\TSQLPP.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\Vegomatic\bin\Debug\vegomatic.exe" "%LT_TOOLBIN%"
copy "%LT_TOOLS%\Wipe\bin\Debug\Wipe.exe" "%LT_TOOLBIN%"

copy "%LT_TOOLS%\VSTasks\bin\Debug\*.dll" "%LT_TOOLBIN%"

pause