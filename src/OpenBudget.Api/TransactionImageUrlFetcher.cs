using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace OpenBudget.Api;

/// <summary>Thrown for any URL-import failure. The message is always generic; details are never returned to the client.</summary>
public sealed class ImageFetchException(string reason) : Exception(reason)
{
    public const string ClientMessage = "The image could not be downloaded from that address. Check it is a public https link to a JPEG, PNG, WebP or GIF image.";
}

public interface ITransactionImageUrlFetcher
{
    /// <summary>Downloads at most <paramref name="maxBytes"/> bytes from a validated public URL.</summary>
    Task<byte[]> FetchAsync(Uri url, long maxBytes, CancellationToken ct);
}

/// <summary>Classifies destination addresses for server-side fetches. Only public unicast addresses are allowed.</summary>
public static class SsrfAddressPolicy
{
    private static readonly (uint Network, int Prefix)[] BlockedV4 =
    [
        (0x00000000, 8),   // 0.0.0.0/8 "this network"
        (0x0A000000, 8),   // 10.0.0.0/8 private
        (0x64400000, 10),  // 100.64.0.0/10 carrier-grade NAT
        (0x7F000000, 8),   // 127.0.0.0/8 loopback
        (0xA9FE0000, 16),  // 169.254.0.0/16 link-local, including 169.254.169.254 cloud metadata
        (0xAC100000, 12),  // 172.16.0.0/12 private
        (0xC0000000, 24),  // 192.0.0.0/24 IETF protocol assignments
        (0xC0000200, 24),  // 192.0.2.0/24 TEST-NET-1
        (0xC0586300, 24),  // 192.88.99.0/24 6to4 relay anycast
        (0xC0A80000, 16),  // 192.168.0.0/16 private
        (0xC6120000, 15),  // 198.18.0.0/15 benchmarking
        (0xC6336400, 24),  // 198.51.100.0/24 TEST-NET-2
        (0xCB007100, 24),  // 203.0.113.0/24 TEST-NET-3
        (0xE0000000, 4),   // 224.0.0.0/4 multicast
        (0xF0000000, 4),   // 240.0.0.0/4 reserved + 255.255.255.255 broadcast
    ];

    private static readonly (byte[] Network, int Prefix)[] BlockedV6InGlobal =
    [
        (IPAddress.Parse("2001::").GetAddressBytes(), 23),      // IETF protocol assignments incl. Teredo and ORCHID
        (IPAddress.Parse("2001:db8::").GetAddressBytes(), 32),  // documentation
        (IPAddress.Parse("2002::").GetAddressBytes(), 16),      // 6to4 (embeds arbitrary IPv4)
        (IPAddress.Parse("3fff::").GetAddressBytes(), 20),      // documentation
    ];

    public static bool IsBlocked(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var value = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(address.GetAddressBytes(), 0));
            return BlockedV4.Any(range => (value & Mask(range.Prefix)) == range.Network);
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return true;
        if (address.IsIPv4MappedToIPv6) return IsBlocked(address.MapToIPv4());
        var bytes = address.GetAddressBytes();
        // Only global unicast (2000::/3) is reachable; this excludes ::, ::1, IPv4-compatible ::/96, NAT64 64:ff9b::/96,
        // discard 100::/64, ULA fc00::/7, link-local fe80::/10, site-local fec0::/10 and multicast ff00::/8.
        if ((bytes[0] & 0xE0) != 0x20) return true;
        return BlockedV6InGlobal.Any(range => InPrefix(bytes, range.Network, range.Prefix));
    }

    private static uint Mask(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);

    private static bool InPrefix(byte[] address, byte[] network, int prefix)
    {
        for (var bit = 0; bit < prefix; bit++)
        {
            var mask = (byte)(0x80 >> (bit % 8));
            if ((address[bit / 8] & mask) != (network[bit / 8] & mask)) return false;
        }
        return true;
    }
}

public sealed class TransactionImageUrlFetcherOptions
{
    /// <summary>Only https on port 443 is allowed. Tests may relax the port so a local listener proves the address guard.</summary>
    public bool AllowNonDefaultPort { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int MaxRedirects { get; init; } = 3;
}

/// <summary>
/// SSRF-hardened fetcher: https only on the default port, no credentials, no proxy, DNS resolved and every resolved
/// address validated inside <see cref="SocketsHttpHandler.ConnectCallback"/> (the socket connects only to a validated IP,
/// defeating DNS rebinding), manual redirects re-validated per hop, a short overall timeout and a hard streamed byte cap.
/// </summary>
public sealed class SafeTransactionImageUrlFetcher : ITransactionImageUrlFetcher, IDisposable
{
    private readonly HttpClient client;
    private readonly TransactionImageUrlFetcherOptions options;
    private readonly ILogger<SafeTransactionImageUrlFetcher> logger;

    public SafeTransactionImageUrlFetcher(TransactionImageUrlFetcherOptions options, ILogger<SafeTransactionImageUrlFetcher> logger)
    {
        this.options = options;
        this.logger = logger;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            ConnectCallback = ConnectToPublicAddressAsync,
        };
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = TransactionImageValidator.MaxImageBytes + 1 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenBudget-ImageImport/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
    }

    public void Dispose() => client.Dispose();

    public static string? ValidateUrl(Uri url, bool allowNonDefaultPort = false)
    {
        if (!url.IsAbsoluteUri) return "relative";
        if (url.Scheme != Uri.UriSchemeHttps) return "scheme";
        if (!string.IsNullOrEmpty(url.UserInfo)) return "credentials";
        if (!url.IsDefaultPort && !allowNonDefaultPort) return "port";
        if (string.IsNullOrWhiteSpace(url.DnsSafeHost)) return "host";
        if (IPAddress.TryParse(url.DnsSafeHost, out var literal) && SsrfAddressPolicy.IsBlocked(literal)) return "blocked-address";
        return null;
    }

    public async Task<byte[]> FetchAsync(Uri url, long maxBytes, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Timeout);
        var current = url;
        try
        {
            for (var hop = 0; ; hop++)
            {
                var problem = ValidateUrl(current, options.AllowNonDefaultPort);
                if (problem is not null) throw new ImageFetchException(problem);
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    if (hop >= options.MaxRedirects) throw new ImageFetchException("too-many-redirects");
                    current = location.IsAbsoluteUri ? location : new Uri(current, location);
                    continue;
                }
                if (response.StatusCode != HttpStatusCode.OK) throw new ImageFetchException("status");
                if (response.Content.Headers.ContentLength > maxBytes) throw new ImageFetchException("too-large");
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
                {
                    if (buffer.Length + read > maxBytes) throw new ImageFetchException("too-large");
                    buffer.Write(chunk, 0, read);
                }
                return buffer.ToArray();
            }
        }
        catch (ImageFetchException error)
        {
            // Log only the host; paths and query strings can carry tokens.
            logger.LogInformation("Image URL import from host {Host} was rejected: {Reason}", current.IdnHost, error.Message);
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException or SocketException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogInformation("Image URL import from host {Host} failed: {Reason}", current.IdnHost, error.GetType().Name);
            throw new ImageFetchException("network");
        }
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
        // Reject when any answer is internal so a mixed public/private record set cannot be used to reach internal hosts.
        if (addresses.Length == 0 || addresses.Any(SsrfAddressPolicy.IsBlocked)) throw new HttpRequestException("Destination address is not allowed.");
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
            if (socket.RemoteEndPoint is not IPEndPoint remote || SsrfAddressPolicy.IsBlocked(remote.Address))
                throw new HttpRequestException("Destination address is not allowed.");
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
