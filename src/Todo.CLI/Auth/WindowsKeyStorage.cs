using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Todo.CLI.Auth;

[SupportedOSPlatform("windows")]
public class WindowsKeyStorage : IKeyStorage
{
    private const string KeyName = "Todo.CLI.EncryptionKey";

    public byte[] GetKey()
    {
        byte[] protectedKey;
        try
        {
            protectedKey = File.ReadAllBytes(GetKeyPath());
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new KeyNotFoundException("No key has been saved yet.", ex);
        }

        return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
    }

    public void SaveKey(byte[] key)
    {
        var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        var keyPath = GetKeyPath();
        var directory = Path.GetDirectoryName(keyPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(keyPath, protectedKey);
    }

    private static string GetKeyPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Todo.CLI",
            "key.bin"
        );
    }
}
