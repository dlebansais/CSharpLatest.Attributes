nuget install dlebansais.Packager -DependencyVersion Highest -OutputDirectory packages
packages\dlebansais.Packager.2.1.1\lib\net481\Packager.exe --prefix:dlebansais
nuget pack nuget\dlebansais.CSharpLatest.Attributes.nuspec  
pause