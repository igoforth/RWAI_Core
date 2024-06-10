namespace AICore;

public class UnityWebRequestException : Exception
{
    public UnityWebRequestException() : base() { }
    public UnityWebRequestException(string message) : base(message) { }
    public UnityWebRequestException(string message, Exception innerException) : base(message, innerException) { }
}