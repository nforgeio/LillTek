REM This batch file creates all of the local IIS sites required
REM for developing the LillTek Platform and Projects.

@echo on
set PHONEROOT=%EN_ROOT%\Intelius\Phone

REM Make sure that ASP.NET is registered with IIS (64-bit only).

cd %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\
aspnet_regiis.exe -i

REM Global Sites

vegomatic iis siteadd LillTek-WebTest "%LT_ROOT%\LillTek\Test\Web" http://127.0.0.1:80

pause
