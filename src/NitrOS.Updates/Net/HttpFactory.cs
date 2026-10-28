using System.Net.Http.Headers;

namespace NitrOS.Updates.Net;

public static class HttpFactory
{
    public static HttpClient Create(HttpMessageHandler? handler = null)
    {
        var h = handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false
        };

        var client = new HttpClient(h, disposeHandler: handler == null)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NitrOS", "1.0"));
        return client;
    }
}
