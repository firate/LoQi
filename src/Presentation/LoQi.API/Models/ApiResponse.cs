using System.Text.Json.Serialization;
using LoQi.Application.Common;

namespace LoQi.API.Models;

public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public List<ApiError>? Errors { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo Pagination { get; init; }

    // Private constructor - factory methods kullanılmalı
    public ApiResponse(bool success, T? data, string? error, List<ApiError>? errors, PaginationInfo pagination)
    {
        Success = success;
        Data = data;
        Error = error;
        Errors = errors;
        Pagination = pagination;
    }

    //  Success factory methods
    public static ApiResponse<T> Ok(T data)
        => new(true, data, null, null, PaginationInfo.Empty());

    public static ApiResponse<T> OkWithPagination(T data, PaginationInfo pagination)
        => new(true, data, null, null, pagination);

    //  Error factory methods
    public static ApiResponse<T> Fail(string error)
        => new(false, default, error, null, PaginationInfo.Empty());

    public static ApiResponse<T> Fail(string error, List<ApiError> errors)
        => new(false, default, error, errors, PaginationInfo.Empty());

    public static ApiResponse<T> Fail(Exception exception)
        => new(false, default, exception.Message, null, PaginationInfo.Empty());

    //  HTTP Status specific methods
    public static ApiResponse<T> BadRequest(string? message = null)
        => new(false, default, message ?? "Bad request", null, PaginationInfo.Empty());

    public static ApiResponse<T> BadRequest(string message, List<ApiError> validationErrors)
        => new(false, default, message, validationErrors, PaginationInfo.Empty());

    public static ApiResponse<T> NotFound(string? message = null)
        => new(false, default, message ?? "Resource not found", null, PaginationInfo.Empty());

    public static ApiResponse<T> Unauthorized(string? message = null)
        => new(false, default, message ?? "Unauthorized access", null, PaginationInfo.Empty());

    //  Validation error için özel method (BadRequest'in alias'ı gibi)
    public static ApiResponse<T> ValidationError(string message, List<ApiError> validationErrors)
        => new(false, default, message, validationErrors, PaginationInfo.Empty());
}