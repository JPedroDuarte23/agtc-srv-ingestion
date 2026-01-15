using System.Net;
using System.Text.Json;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using AgtcSrvIngestion.Application.Exceptions;

namespace AgtcSrvIngestion.Infrastructure.Middleware;

public class ExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandler> _logger;

    public ExceptionHandler(RequestDelegate next, ILogger<ExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            await HandleExceptionAsync(
                context, 
                ex, 
                HttpStatusCode.Conflict, 
                "O e-mail já está cadastrado.", 
                "Conflict"
            );
        }
        catch (HttpException ex)
        {
            await HandleExceptionAsync(
                context, 
                ex, 
                (HttpStatusCode)ex.StatusCode, 
                ex.Message, 
                ex.Error
            );
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(
                context, 
                ex, 
                HttpStatusCode.InternalServerError, 
                ex.Message, 
                "Internal Server Error"
            );
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex, HttpStatusCode statusCode, string message, string error)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var traceId = context.Items["X-Correlation-ID"]?.ToString() ?? context.TraceIdentifier;

        var response = new
        {
            error,
            statusCode = (int)statusCode,
            message,
            traceId
        };

        _logger.LogError(ex, "Ocorreu uma exceção do tipo {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}
