using System.Net;

namespace NitrOS.Updates.Tests.Fakes;

public sealed class FakeHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage>? OnSend { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (OnSend != null)
        {
            return Task.FromResult(OnSend(request));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
