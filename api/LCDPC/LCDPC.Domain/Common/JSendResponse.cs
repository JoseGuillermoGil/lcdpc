namespace LCDPC.Domain.Common;

public sealed class JSendResponse<T>
{
    public string Status { get; private set; } = "success";
    public T? Data { get; private set; }
    public string? Message { get; private set; }

    public static JSendResponse<T> Success(T data) => new()
    {
        Status = "success",
        Data = data
    };

    public static JSendResponse<T> Fail(string message) => new()
    {
        Status = "fail",
        Message = message
    };
}
