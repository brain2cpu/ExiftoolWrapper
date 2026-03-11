# ExiftoolWrapper


A .NET wrapper over Phil Harvey's ExifTool  
[https://sno.phy.queensu.ca/~phil/exiftool/](https://sno.phy.queensu.ca/~phil/exiftool/)


Do not forget to add  
**exiftool(-k).exe**  
to your project output folder or specify the full path in the constructor!  
Use exiftool version 13 or later!


A simple example:


```C#
using var etw = new ExifToolWrapper(@"c:\Work\TestProject\exiftool(-k).exe");
etw.Start();
Dictionary<string, string> d = etw.FetchExifFrom(@"d:\Photos\20180101-000003.jpg");
etw.Stop();
```


More usage scenarios can be found in the test project.

For more info visit  
[https://github.com/brain2cpu/ExiftoolWrapper](https://github.com/brain2cpu/ExiftoolWrapper)

NuGet package is available:  
[https://www.nuget.org/packages/ExiftoolWrapper/](https://www.nuget.org/packages/ExiftoolWrapper/)
