using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Todo.CLI.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

static class TodoCliAuthenticationProviderFactory
{
    public static IAuthenticationProvider GetAuthenticationProvider(IServiceProvider factory)
    {
        var config = factory.GetRequiredService<TodoCliConfiguration>();

        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithRedirectUri("http://localhost") // Only loopback redirect uri is supported, see https://aka.ms/msal-net-os-browser for details
            .Build();

        TokenCacheHelper.EnableSerialization(app.UserTokenCache);

        // Parallel requests must not each open an interactive sign-in.
        var gate = new SemaphoreSlim(1, 1);

        async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Silent acquisition returns the cached token or refreshes it, so long sessions keep working.
                var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
                if (accounts.Any())
                {
                    try
                    {
                        var silent = await app.AcquireTokenSilent(config.Scopes, accounts.First())
                            .ExecuteAsync(cancellationToken)
                            .ConfigureAwait(false);
                        return silent.AccessToken;
                    }
                    catch (MsalUiRequiredException)
                    {
                        // Refresh token expired or revoked; fall through to interactive login.
                    }
                }

                var interactive = await app.AcquireTokenInteractive(config.Scopes)
                    .WithPrompt(Prompt.NoPrompt)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
                return interactive.AccessToken;
            }
            finally
            {
                gate.Release();
            }
        }

        return new MsalAuthenticationProvider(GetAccessTokenAsync);
    }
}
