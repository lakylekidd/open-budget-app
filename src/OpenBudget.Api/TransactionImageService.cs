using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record TransactionImageDto(Guid Id, Guid TransactionId, string FileName, string ContentType, long SizeBytes, int Width, int Height,
    string Source, DateTimeOffset CreatedAt, string Url);
public sealed record TransactionImageListDto(Guid TransactionId, int MaxImages, List<TransactionImageDto> Images);
public sealed record ImportTransactionImageUrlRequest(Guid CommandId, string? Url);
public sealed record DeleteTransactionImageRequest(Guid CommandId);
public sealed record TransactionImageUpload(string? FileName, byte[] Content);
public sealed record TransactionImageContent(TransactionImageEntity Image, byte[] Content);

/// <summary>
/// Transaction image attachments. Images are non-financial: changes write a ledger audit receipt at the current ledger
/// version (metadata only, never bytes or URLs) and never bump the version or recalculate balances.
/// </summary>
public sealed class TransactionImageService(BudgetDbContext db, LedgerGate gate, ITransactionImageStore store, ITransactionImageUrlFetcher fetcher, TimeProvider clock)
{
    public const int MaxImagesPerTransaction = 20;
    public const int MaxFilesPerRequest = 10;
    public const int MaxUrlLength = 2048;

    public async Task<TransactionImageListDto> ListAsync(Guid transactionId, CancellationToken ct)
    {
        await RequireTransactionAsync(transactionId, ct);
        return await ToListAsync(transactionId, ct);
    }

    public async Task<TransactionImageListDto> UploadAsync(Guid transactionId, Guid commandId, IReadOnlyList<TransactionImageUpload> uploads, string? userId, CancellationToken ct)
    {
        if (commandId == Guid.Empty) throw new LedgerException("Command ID is required.");
        if (uploads.Count == 0) throw new LedgerException("Choose at least one image.");
        if (uploads.Count > MaxFilesPerRequest) throw new LedgerException($"Upload at most {MaxFilesPerRequest} images at a time.");
        var validated = uploads.Select((upload, index) => (Upload: upload, Image: ValidateOrThrow(upload.Content, uploads.Count == 1 ? "" : $"Image {index + 1}: "))).ToList();
        return await AddAsync(transactionId, commandId, "upload", validated.Select(x => (ImageFileNameSanitizer.Sanitize(x.Upload.FileName, x.Image.Extension), x.Image)).ToList(),
            string.Join(",", validated.Select(x => Hash(x.Image.Content))), userId, ct);
    }

    public async Task<TransactionImageListDto> ImportFromUrlAsync(Guid transactionId, ImportTransactionImageUrlRequest request, string? userId, CancellationToken ct)
    {
        if (request.CommandId == Guid.Empty) throw new LedgerException("Command ID is required.");
        var text = request.Url?.Trim() ?? "";
        if (text.Length is 0 or > MaxUrlLength || !Uri.TryCreate(text, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            throw new LedgerException("Enter a public https:// link to an image.");
        // Exact replay must not fetch again.
        var fingerprint = Fingerprint(AddKind(transactionId), "url:" + Hash(Encoding.UTF8.GetBytes(url.AbsoluteUri)));
        if (await ReplayAsync(request.CommandId, fingerprint, ct)) return await ToListAsync(transactionId, ct);
        await RequireTransactionAsync(transactionId, ct);
        byte[] content;
        try { content = await fetcher.FetchAsync(url, TransactionImageValidator.MaxImageBytes, ct); }
        catch (ImageFetchException) { throw new LedgerException(ImageFetchException.ClientMessage, 422); }
        var image = ValidateOrThrow(content, "");
        var name = ImageFileNameSanitizer.Sanitize(Uri.UnescapeDataString(url.Segments.LastOrDefault() ?? ""), image.Extension);
        return await AddAsync(transactionId, request.CommandId, "url", [(name, image)], "url:" + Hash(Encoding.UTF8.GetBytes(url.AbsoluteUri)), userId, ct);
    }

    public Task<bool> DeleteAsync(Guid imageId, DeleteTransactionImageRequest request, CancellationToken ct) =>
        gate.RunAsync(async () =>
        {
            if (request.CommandId == Guid.Empty) throw new LedgerException("Command ID is required.");
            var kind = $"transaction.image.delete/{imageId}";
            var fingerprint = Fingerprint(kind, imageId.ToString());
            if (await ReplayAsync(request.CommandId, fingerprint, ct)) return true;
            var image = await VisibleImages().SingleOrDefaultAsync(x => x.Id == imageId, ct) ?? throw new LedgerException("Image not found.", 404);
            await store.DeleteAsync(db.ActiveBudgetId, image.Id, ct);
            db.TransactionImages.Remove(image);
            await AddReceiptAsync(request.CommandId, image.TransactionId, kind, fingerprint, new { images = new[] { Audit(image) } }, new { deleted = true }, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task<TransactionImageContent> ReadAsync(Guid imageId, CancellationToken ct)
    {
        var image = await VisibleImages().AsNoTracking().SingleOrDefaultAsync(x => x.Id == imageId, ct) ?? throw new LedgerException("Image not found.", 404);
        var content = await store.ReadAsync(db.ActiveBudgetId, imageId, ct) ?? throw new LedgerException("Image not found.", 404);
        return new TransactionImageContent(image, content);
    }

    private async Task<TransactionImageListDto> AddAsync(Guid transactionId, Guid commandId, string source, List<(string Name, ValidatedImage Image)> images, string payload, string? userId, CancellationToken ct) =>
        await gate.RunAsync(async () =>
        {
            var kind = AddKind(transactionId);
            var fingerprint = Fingerprint(kind, payload);
            if (await ReplayAsync(commandId, fingerprint, ct)) return await ToListAsync(transactionId, ct);
            await RequireTransactionAsync(transactionId, ct);
            var existing = await db.TransactionImages.CountAsync(x => x.TransactionId == transactionId, ct);
            if (existing + images.Count > MaxImagesPerTransaction)
                throw new LedgerException($"A transaction can have at most {MaxImagesPerTransaction} images. Delete one before adding more.", 409);
            var now = clock.GetUtcNow();
            var rows = images.Select((item, index) => new TransactionImageEntity
            {
                Id = Guid.NewGuid(), BudgetId = db.ActiveBudgetId, TransactionId = transactionId, FileName = item.Name, ContentType = item.Image.ContentType,
                SizeBytes = item.Image.Content.Length, Width = item.Image.Width, Height = item.Image.Height, Sha256 = Hash(item.Image.Content), Source = source,
                CreatedByUserId = userId, CreatedAt = now.AddTicks(index),
            }).ToList();
            db.TransactionImages.AddRange(rows);
            for (var i = 0; i < rows.Count; i++) await store.SaveAsync(db.ActiveBudgetId, rows[i].Id, images[i].Image.Content, ct);
            await AddReceiptAsync(commandId, transactionId, kind, fingerprint, null, new { images = rows.Select(Audit).ToList() }, ct);
            await db.SaveChangesAsync(ct);
            return await ToListAsync(transactionId, ct);
        }, ct);

    // Images on soft-deleted transactions stay stored but are hidden from every read.
    private IQueryable<TransactionImageEntity> VisibleImages() =>
        db.TransactionImages.Where(image => db.Transactions.Any(t => t.Id == image.TransactionId && !t.IsDeleted));

    private async Task RequireTransactionAsync(Guid transactionId, CancellationToken ct)
    {
        if (!await db.Transactions.AnyAsync(t => t.Id == transactionId && !t.IsDeleted, ct)) throw new LedgerException("Transaction not found.", 404);
    }

    private async Task<TransactionImageListDto> ToListAsync(Guid transactionId, CancellationToken ct)
    {
        var rows = await VisibleImages().AsNoTracking().Where(x => x.TransactionId == transactionId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        return new TransactionImageListDto(transactionId, MaxImagesPerTransaction, rows.Select(ToDto).ToList());
    }

    private async Task<bool> ReplayAsync(Guid commandId, string fingerprint, CancellationToken ct)
    {
        var previous = await db.LedgerCommands.AsNoTracking().SingleOrDefaultAsync(c => c.Id == commandId, ct);
        if (previous is null) return false;
        if (previous.Fingerprint != fingerprint) throw new LedgerException("This command ID was already used for a different request.", 409);
        return true;
    }

    private async Task AddReceiptAsync(Guid commandId, Guid transactionId, string kind, string fingerprint, object? before, object? after, CancellationToken ct)
    {
        // Receipts record the current ledger version without incrementing it: image changes never affect money.
        var version = await db.LedgerStates.AsNoTracking().Select(x => x.Version).SingleAsync(ct);
        db.LedgerCommands.Add(new LedgerCommandEntity
        {
            Id = commandId, ResourceId = transactionId, Kind = kind, Fingerprint = fingerprint, Version = version,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after), CreatedAt = clock.GetUtcNow(),
        });
    }

    private static ValidatedImage ValidateOrThrow(byte[] content, string prefix)
    {
        try { return TransactionImageValidator.Validate(content); }
        catch (ImageValidationException error) { throw new LedgerException(prefix + error.Message, content.Length > TransactionImageValidator.MaxImageBytes ? 413 : 400); }
    }

    private static object Audit(TransactionImageEntity image) => new { transactionId = image.TransactionId, imageId = image.Id, sizeBytes = image.SizeBytes, contentType = image.ContentType, source = image.Source };
    private static TransactionImageDto ToDto(TransactionImageEntity x) => new(x.Id, x.TransactionId, x.FileName, x.ContentType, x.SizeBytes, x.Width, x.Height, x.Source, x.CreatedAt, $"/api/transaction-images/{x.Id}");
    private static string AddKind(Guid transactionId) => $"transaction.image.add/{transactionId}";
    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
    private static string Fingerprint(string kind, string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + payload)));
}
