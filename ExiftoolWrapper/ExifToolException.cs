using System;

namespace Brain2CPU.ExifTool;

[Serializable]
public class ExifToolException(string msg) : Exception(msg);