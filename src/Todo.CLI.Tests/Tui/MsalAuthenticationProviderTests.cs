using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using Todo.CLI.Auth;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class MsalAuthenticationProviderTests
{
    private static RequestInformation NewRequest() => new() { HttpMethod = Method.GET, URI = new Uri("https://graph.microsoft.com/v1.0/me") };

    [Fact]
    public async Task AuthenticateRequestAsync_AddsBearerHeader()
    {
        var provider = new MsalAuthenticationProvider(_ => Task.FromResult("abc"));
        var request = NewRequest();

        await provider.AuthenticateRequestAsync(request);

        Assert.Equal("Bearer abc", Assert.Single(request.Headers["Authorization"]));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_AsksForATokenOnEveryRequestSoExpiredTokensRefresh()
    {
        var calls = 0;
        var provider = new MsalAuthenticationProvider(_ => Task.FromResult($"token-{++calls}"));
        var first = NewRequest();
        var second = NewRequest();

        await provider.AuthenticateRequestAsync(first);
        await provider.AuthenticateRequestAsync(second);

        Assert.Equal("Bearer token-1", Assert.Single(first.Headers["Authorization"]));
        Assert.Equal("Bearer token-2", Assert.Single(second.Headers["Authorization"]));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_ReplacesAnExistingAuthorizationHeader()
    {
        var provider = new MsalAuthenticationProvider(_ => Task.FromResult("new"));
        var request = NewRequest();
        request.Headers.Add("Authorization", "Bearer old");

        await provider.AuthenticateRequestAsync(request);

        Assert.Equal("Bearer new", Assert.Single(request.Headers["Authorization"]));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_TokenSourceFails_Propagates()
    {
        var provider = new MsalAuthenticationProvider(_ => throw new InvalidOperationException("sign-in cancelled"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.AuthenticateRequestAsync(NewRequest()));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_PassesCancellationTokenToTheSource()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        var provider = new MsalAuthenticationProvider(ct => { seen = ct; return Task.FromResult("t"); });

        await provider.AuthenticateRequestAsync(NewRequest(), null, cts.Token);

        Assert.Equal(cts.Token, seen);
    }
}
