using System;
using System.IO;
using Microsoft.Identity.Client;

namespace Todo.CLI.Auth;

static class TokenCacheHelper
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".todo-cli"
    );
    private static readonly string CacheFilePath = Path.Combine(ConfigDir, "token.cache");
    private static readonly EncryptedTokenFile Store = new(CacheFilePath, KeyStorageFactory.Create());

    public static void EnableSerialization(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        var plaintext = Store.Read();
        if (plaintext != null)
        {
            args.TokenCache.DeserializeMsalV3(plaintext);
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
        {
            Store.Write(args.TokenCache.SerializeMsalV3());
        }
    }
}
