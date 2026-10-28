using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace NitrOS.Updates.Net;

public static class HttpFactory
{
    public static HttpClient Create(HttpMessageHandler? handler = null)
    {
        var h = handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
                var ip = entry.AddressList.FirstOrDefault()
                    ?? throw new SocketException((int)SocketError.HostNotFound);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        var client = new HttpClient(h, disposeHandler: handler == null)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NitrOS", "1.0"));
        return client;
    }
}
