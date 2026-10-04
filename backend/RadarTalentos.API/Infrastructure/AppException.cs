namespace RadarTalentos.API.Infrastructure;

/// <summary>Erro de negócio esperado; vira uma resposta JSON com status e código legíveis pelo frontend.</summary>
public class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static AppException NotFound(string what) => new(404, "NOT_FOUND", $"{what} não encontrado(a).");
    public static AppException BadRequest(string message, string code = "VALIDATION") => new(400, code, message);
    public static AppException Conflict(string message, string code = "CONFLICT") => new(409, code, message);
}

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await Write(context, ex.Status, ex.Code, ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await Write(context, 409, "DUPLICATE", "Já existe um registro com esses dados.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Path}", context.Request.Path);
            await Write(context, 500, "INTERNAL", "Erro interno. Tente novamente.");
        }
    }

    public static bool IsUniqueViolation(Exception ex) =>
        ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;

    private static Task Write(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.Clear();
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { status, code, message });
    }
}
