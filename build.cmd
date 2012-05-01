@echo off
cd %~dp0

set EnableNuGetPackageRestore=true
".nuget\NuGet.exe" install Sake -pre -o packages
for /f "tokens=*" %%G in ('dir /AD /ON /B "packages\Sake.*"') do set __sake__=%%G
"packages\%__sake__%\tools\Sake.exe" -f Sakefile.shade -I tools/sake %*
set __sake__=
