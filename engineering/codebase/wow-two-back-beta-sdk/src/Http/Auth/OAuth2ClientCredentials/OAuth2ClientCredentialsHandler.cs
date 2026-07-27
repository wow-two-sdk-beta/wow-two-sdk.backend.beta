using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.OAuth2ClientCredentials;

/// <summary>Delegating handler attaching a cached client-credentials bearer token to outgoing requests lacking an <c>Authorization</c> header.</summary>
internal sealed class OAuth2ClientCredentialsHandler : DelegatingHandler
{
    private readonly string _clientName;
    private readonly OAuth2TokenCache _tokenCache;
    private readonly IOptionsMonitor<OAuth2ClientCredentialsOptions> _optionsMonitor;

    public OAuth2ClientCredentialsHandler(
        string clientName,
        OAuth2TokenCache tokenCache,
        IOptionsMonitor<OAuth2ClientCredentialsOptions> optionsMonitor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentNullException.ThrowIfNull(tokenCache);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        _clientName = clientName;
        _tokenCache = tokenCache;
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.Authorization is null)
        {
            var options = _optionsMonitor.Get(_clientName);
            var token = await _tokenCache.GetAccessTokenAsync(_clientName, options, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
