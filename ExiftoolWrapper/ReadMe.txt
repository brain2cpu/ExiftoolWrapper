ExiftoolWrapper - a .NET wrapper over Phil Harvey's ExifTool.

This package does not contain the ExifTool executable, please download the latest version from
https://sno.phy.queensu.ca/~phil/exiftool/

Do not forget to add 
exiftool(-k).exe
to your project output folder or specify the full path in the constructor!

A simple example:

Dictionary<string, string> d;
using(var etw = new ExifToolWrapper(@"c:\Work\TestProject\exiftool(-k).exe"))
{
    etw.Start();
    d = etw.FetchExifFrom(@"d:\Photos\20180101-000003.jpg");
}

For more info visit
https://github.com/brain2cpu/ExiftoolWrapper
or
http://u88.n24.queensu.ca/exiftool/forum/index.php/topic,5262.0.html
