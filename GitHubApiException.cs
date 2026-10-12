namespace tia_manager;

public sealed class GitHubApiException(
    string message,
    int statusCode,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
