using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Todo.CLI.Auth;

/// <summary>
/// Adds a bearer token to every request, asking the token source each time so expired tokens are refreshed.
/// A token captured once at startup would expire mid-session in long-running use such as the TUI.
/// </summary>
internal sealed class MsalAuthenticationProvider : IAuthenticationProvider
{
    private const string AuthorizationHeader = "Authorization";
    private readonly Func<CancellationToken, Task<string>> _getAccessToken;

    public MsalAuthenticationProvider(Func<CancellationToken, Task<string>> getAccessToken)
    {
        _getAccessToken = getAccessToken;
    }

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var token = await _getAccessToken(cancellationToken).ConfigureAwait(false);
        request.Headers.Remove(AuthorizationHeader);
        request.Headers.Add(AuthorizationHeader, "Bearer " + token);
    }
}
