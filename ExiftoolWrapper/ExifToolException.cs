using System;

namespace Brain2CPU.ExifTool
{
    [Serializable]
    public class ExifToolException : Exception
    {
        public ExifToolException(string msg) : base(msg)
        {}
    }
}
