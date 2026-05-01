namespace CaseGig.Api.Contracts;

public sealed class ApiResponse<T>
{
    public required bool Success { get; init; }
    public required T? Data { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }

    public static ApiResponse<T> Ok(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Errors = Array.Empty<string>()
        };
    }

    public static ApiResponse<T> Fail(params string[] errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Errors = errors
        };
    }
}
