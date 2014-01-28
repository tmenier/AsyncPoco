..\.nuget\nuget pack "NuGet\AsyncPoco.nuspec" -o "..\Output"

..\csj\csj.exe -o:AsyncPoco.cs Database.cs -r *.cs -x:Properties\*.cs
