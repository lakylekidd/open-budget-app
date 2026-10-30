using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

namespace OpenBudget.Api;

public static class TransactionImageEndpoints
{
    /// <summary>Hard cap for one multipart upload request (up to 10 images of at most 10 MB plus form overhead).</summary>
    public const long MaxUploadRequestBytes = 50L * 1024 * 1024;
    public const string UrlImportRateLimitPolicy = "transaction-image-url";

    public static IServiceCollection AddTransactionImages(this IServiceCollection services)
    {
        services.AddScoped<TransactionImageService>();
        services.AddScoped<ITransactionImageStore, PostgresTransactionImageStore>();
        services.AddSingleton(new TransactionImageUrlFetcherOptions());
        services.AddSingleton<ITransactionImageUrlFetcher, SafeTransactionImageUrlFetcher>();
        // URL imports make outbound requests, so they are throttled per signed-in user to limit scanning abuse.
        services.AddRateLimiter(options => options.AddPolicy(UrlImportRateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }

    public static void MapTransactionImageEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/transactions/{id:guid}/images", (Guid id, TransactionImageService images, CancellationToken ct) => images.ListAsync(id, ct));
        api.MapPost("/transactions/{id:guid}/images", UploadAsync);
        api.MapPost("/transactions/{id:guid}/images/from-url", (Guid id, ImportTransactionImageUrlRequest request, ClaimsPrincipal user, TransactionImageService images, CancellationToken ct) =>
            images.ImportFromUrlAsync(id, request, UserId(user), ct)).RequireRateLimiting(UrlImportRateLimitPolicy);
        api.MapGet("/transaction-images/{imageId:guid}", DownloadAsync);
        api.MapPost("/transaction-images/{imageId:guid}/delete", async (Guid imageId, DeleteTransactionImageRequest request, TransactionImageService images, CancellationToken ct) =>
        {
            await images.DeleteAsync(imageId, request, ct);
            return Results.Ok(new { id = imageId });
        });
    }

    private static async Task<IResult> UploadAsync(Guid id, HttpContext context, ClaimsPrincipal user, TransactionImageService images, CancellationToken ct)
    {
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaxUploadRequestBytes;
        if (context.Request.ContentLength > MaxUploadRequestBytes) return TooLarge("Upload at most 50 MB at a time.");
        if (!context.Request.HasFormContentType) return Results.BadRequest(new { error = "Upload images as multipart/form-data." });
        // Keep the whole (capped) body in memory so receipts are never buffered to temporary files on disk.
        context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions
        {
            MultipartBodyLengthLimit = MaxUploadRequestBytes, BufferBodyLengthLimit = MaxUploadRequestBytes, MemoryBufferThreshold = (int)MaxUploadRequestBytes,
            ValueCountLimit = 2 * TransactionImageService.MaxFilesPerRequest + 4, ValueLengthLimit = 200, KeyLengthLimit = 40, MultipartHeadersLengthLimit = 16 * 1024,
        }));
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return TooLarge("Upload at most 50 MB at a time."); }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge) { return TooLarge("Upload at most 50 MB at a time."); }
        if (!Guid.TryParse(form["commandId"].ToString(), out var commandId)) return Results.BadRequest(new { error = "Command ID is required." });
        var files = form.Files.GetFiles("files");
        if (files.Count > TransactionImageService.MaxFilesPerRequest)
            return Results.BadRequest(new { error = $"Upload at most {TransactionImageService.MaxFilesPerRequest} images at a time." });
        var uploads = new List<TransactionImageUpload>(files.Count);
        foreach (var file in files)
        {
            if (file.Length > TransactionImageValidator.MaxImageBytes) return TooLarge("Each image must be 10 MB or smaller.");
            using var buffer = new MemoryStream((int)file.Length);
            await using (var stream = file.OpenReadStream()) await stream.CopyToAsync(buffer, ct);
            uploads.Add(new TransactionImageUpload(file.FileName, buffer.ToArray()));
        }
        return Results.Ok(await images.UploadAsync(id, commandId, uploads, UserId(user), ct));
    }

    private static async Task<IResult> DownloadAsync(Guid imageId, HttpContext context, TransactionImageService images, CancellationToken ct)
    {
        var result = await images.ReadAsync(imageId, ct);
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        // The stored name is sanitized to [A-Za-z0-9 _-().] so it cannot break out of the quoted parameter.
        headers.ContentDisposition = $"inline; filename=\"{result.Image.FileName}\"";
        headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; sandbox";
        headers.CacheControl = "private, max-age=3600";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        return Results.Bytes(result.Content, result.Image.ContentType);
    }

    private static IResult TooLarge(string message) => Results.Json(new { error = message }, statusCode: StatusCodes.Status413PayloadTooLarge);
    private static string? UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
