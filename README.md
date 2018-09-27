# ExiftoolWrapper
A .NET wrapper over Phil Harvey's ExifTool  
https://sno.phy.queensu.ca/~phil/exiftool/

Do not forget to add  
exiftool(-k).exe  
to your project output folder or specify the full path in the constructor!

A simple example:

```C#
Dictionary<string, string> d;
using(var etw = new ExifToolWrapper(@"c:\Work\TestProject\exiftool(-k).exe"))
{
    etw.Start();
    d = etw.FetchExifFrom(@"d:\Photos\20180101-000003.jpg");
}
```

More usage scenarios can be found in the test project.

NuGet package is available:  
https://www.nuget.org/packages/ExiftoolWrapper/
