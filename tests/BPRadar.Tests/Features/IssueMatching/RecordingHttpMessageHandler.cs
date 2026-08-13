using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace BPRadar.Tests.Features.IssueMatching;

internal sealed class RecordingHttpMessageHandler(string responseBody)
    : HttpMessageHandler
{
    private Dictionary<string, string> headers =
        new(StringComparer.OrdinalIgnoreCase);

    public Uri? RequestUri { get; private set; }

    public AuthenticationHeaderValue? Authorization { get; private set; }

    public string RequestBody { get; private set; } = string.Empty;

    public string? GetHeaderValue(string name)
    {
        return headers.TryGetValue(name, out var value) ? value : null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        Authorization = request.Headers.Authorization;
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        headers = request.Headers
            .ToDictionary(
                header => header.Key,
                header => header.Value.Single(),
                StringComparer.OrdinalIgnoreCase);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json")
        };
    }
}
