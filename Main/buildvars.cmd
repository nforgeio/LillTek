@echo on
REM Configures the environment variables required to build LillTek solutions.
REM 
REM 	buildvars [ <source folder> ]
REM
REM Note that <source folder> defaults to the folder holding this
REM batch file.
REM
REM This must be RUN AS ADMINISTRATOR.

REM Default LT_ROOT to the folder holding this batch file after stripping
REM off the trailing backslash.

set LT_ROOT=%~dp0 
set LT_ROOT=%LT_ROOT:~0,-2%

if not [%1]==[] set LT_ROOT=%1

if exist %LT_ROOT%\Platform\Platform.sln goto goodPath
echo The [%LT_ROOT%\Platform\Platform.sln] file does not exist.  Please pass 
echo the path to the parent of the LillTek Platform solution folder.
goto done

:goodPath

REM ---------------------------------------------------------------------------
REM Platform Setup

set LT_ROOT=C:\LillTek\Main
set LT_TOOLBIN=%LT_ROOT%\ToolBin
set LT_TESTBIN=%LT_ROOT%\TestBin
set LT_BUILD=%LT_ROOT%\Build

set NEONSWITCH=C:\NeonSwitch
set NEONSWITCH_DEBUGBIN=%NEONSWITCH%\x64\Debug
set NEONSWITCH_RELEASEBIN=%NEONSWITCH%\x64\Release
set NEONSWITCH_DEBUGMOD=%NEONSWITCH_DEBUGBIN%\mod
set NEONSWITCH_RELEASEMOD=%NEONSWITCH_RELEASEBIN%\mod
set NEONSWITCH_DEBUGMANAGED=%NEONSWITCH_DEBUGMOD%\managed
set NEONSWITCH_RELEASEMANAGED=%NEONSWITCH_RELEASEMOD%\managed

setx LT_ROOT %LT_ROOT% /M
setx LT_TOOLBIN %LT_TOOLBIN% /M
setx LT_TESTBIN %LT_TESTBIN% /M
setx LT_BUILD %LT_BUILD% /M
setx LT_BUILD_DBPACK 1 /M
setx LT_TEMP C:\Temp /M
setx LT_TEST_DB "server=localhost;Integrated Security=SSPI;Application Name=UnitTest" /M

setx NEONSWITCH %NEONSWITCH% /M
setx NEONSWITCH_DEBUGBIN %NEONSWITCH_DEBUGBIN% /M
setx NEONSWITCH_RELEASEBIN %NEONSWITCH_RELEASEBIN% /M
setx NEONSWITCH_DEBUGMOD %NEONSWITCH_DEBUGMOD% /M
setx NEONSWITCH_RELEASEMOD %NEONSWITCH_RELEASEMOD% /M
setx NEONSWITCH_DEBUGMANAGED %NEONSWITCH_DEBUGMANAGED% /M
setx NEONSWITCH_RELEASEMANAGED %NEONSWITCH_RELEASEMANAGED% /M

setx DOTNETPATH %WINDIR%\Microsoft.NET\Framework\v4.0.30319 /M
setx DEV_WORKSTATION 1 /M

REM Make sure required folders exist.

if not exist %LT_BUILD% md %LT_BUILD%
if not exist %LT_TEMP% md %LT_TEMP%

REM Configure the PATH

%LT_ROOT%\ToolBin\pathtool -dedup -system -add "%LT_TOOLBIN%"
%LT_ROOT%\ToolBin\pathtool -dedup -system -add "%LT_BUILD%"
%LT_ROOT%\ToolBin\pathtool -dedup -system -add "C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727"
%LT_ROOT%\ToolBin\pathtool -dedup -system -add "C:\Program Files\Microsoft SQL Server\100\Tools\Binn"
%LT_ROOT%\ToolBin\pathtool -dedup -system -add "C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE"

%LT_ROOT%\pathtool -dedup -system -add "%DOTNETPATH%"
%LT_ROOT%\pathtool -dedup -system -add %WINSDKPATH%

REM ---------------------------------------------------------------------------
REM FreeSWITCH Setup

setx FreeSWITCH C:\FreeSWITCH /M
setx FreeSWITCH_BIN %%FreeSWITCH%%\Win32\Debug /M
setx FreeSWITCH_MODULES %%FreeSWITCH_BIN%%\mod /M

pause

