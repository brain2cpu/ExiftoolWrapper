namespace Brain2CPU.ExifTool;

public readonly struct ExifToolResponse(bool isSuccess, string result)
{
    public bool IsSuccess { get; } = isSuccess;
    public string Result { get; } = result;

    public ExifToolResponse(string result) : this(result.ToLowerInvariant().Contains(ExifToolWrapper.SuccessMessage), result)
    {
    }

    //to use ExifToolResponse directly in conditionals (discarding response)
    public static implicit operator bool(ExifToolResponse r) => r.IsSuccess;
}