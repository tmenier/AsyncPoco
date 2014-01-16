cd ..\..
pwd


if %1 == Release NuGet.exe pack "AsyncPoco.nuspec" -o "..\Output"
if %1 == Release NuGet.exe pack "AsyncPoco.Core.nuspec" -o "..\Output"

..\csj\csj.exe -o:AsyncPoco.cs Database.cs -r *.cs -x:Properties\*.cs