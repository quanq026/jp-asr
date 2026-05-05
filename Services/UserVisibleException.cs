namespace JapaneseASR.Services;

public sealed class UserVisibleException : Exception
{
    public UserVisibleException(string message) : base(message)
    {
    }
}
