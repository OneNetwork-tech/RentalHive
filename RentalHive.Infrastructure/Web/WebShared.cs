using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalHive.Application.Contracts;
using RentalHive.Domain.Common;
using RentalHive.Infrastructure.Persistence;
using RentalHive.Infrastructure.Storage;

namespace RentalHive.Infrastructure.Web;

/// <summary>Maps business-rule violations to RFC 7807 problem responses.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException ex) return false;
        var status = ex.Kind switch
        {
            DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
            DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = ex.Message },
            Exception = exception,
        }).ConfigureAwait(false);
    }
}

public static class WebShared
{
    /// <summary>Serve listing photos from the image store with safe headers.</summary>
    public static IEndpointRouteBuilder MapMedia(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/{fileName}", (string fileName, IImageStore store, HttpContext http) =>
        {
            if (!LocalImageStore.IsValidName(fileName)) return Results.NotFound();
            var path = store.ResolvePath(fileName);
            if (path is null || !File.Exists(path)) return Results.NotFound();
            var type = fileName.EndsWith(".png", StringComparison.Ordinal) ? "image/png"
                : fileName.EndsWith(".webp", StringComparison.Ordinal) ? "image/webp" : "image/jpeg";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            return Results.File(path, type);
        }).AllowAnonymous();
        return app;
    }

    /// <summary>Liveness + database readiness.</summary>
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (RentalHiveDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct).ConfigureAwait(false)
                ? Results.Ok(new { status = "ok" })
                : Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();
        return app;
    }

    /// <summary>Common security headers for both the web app and the API.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, string contentSecurityPolicy)
    {
        return app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h.ContentSecurityPolicy = contentSecurityPolicy;
            await next().ConfigureAwait(false);
        });
    }

    public static IServiceCollection AddDomainProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();
        return services;
    }
}

public static class UrlSafety
{
    /// <summary>Only allow local paths for post-login redirects (blocks //evil.example, /\evil, absolute URLs).</summary>
    public static string LocalOrRoot(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.StartsWith("/\\", StringComparison.Ordinal) && !url.Contains('\n', StringComparison.Ordinal) && !url.Contains('\r', StringComparison.Ordinal)
            ? url
            : "/";
}
