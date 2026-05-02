using CaseGig.Api.Contracts;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net;

namespace CaseGig.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "API: Falha de validação de negócio");
            await WriteErrorAsync(context, HttpStatusCode.UnprocessableEntity, ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "API: Conflito de concorrência");
            await WriteErrorAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "API: Falha ao persistir alterações no banco de dados");

            var env = context.RequestServices.GetService<IHostEnvironment>();
            var message = env?.IsDevelopment() == true
                ? (ex.InnerException?.Message ?? ex.Message)
                : "Erro ao persistir dados no banco de dados.";

            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, message);
        }
        catch (DbException ex)
        {
            _logger.LogError(ex, "API: Erro de banco de dados");

            var env = context.RequestServices.GetService<IHostEnvironment>();
            var message = env?.IsDevelopment() == true
                ? ex.Message
                : "Erro ao acessar o banco de dados.";

            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API: Erro não tratado");

            var env = context.RequestServices.GetService<IHostEnvironment>();
            var message = env?.IsDevelopment() == true
                ? ex.Message
                : "Ocorreu um erro inesperado.";

            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, params string[] errors)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object>.Fail(errors);
        await context.Response.WriteAsJsonAsync(payload);
    }
}
