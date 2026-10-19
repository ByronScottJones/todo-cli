using System;
using System.IO;
using System.Security.Cryptography;

namespace Todo.CLI.Auth;

/// <summary>
/// Stores the serialized MSAL token cache in a file encrypted with AES-GCM. The key lives in platform key storage,
/// never next to the file. Layout on disk: 12-byte nonce, 16-byte tag, then the ciphertext.
/// </summary>
internal sealed class EncryptedTokenFile
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly string _path;
    private readonly IKeyStorage _keyStorage;
    private readonly object _lock = new();
    private byte[]? _cachedKey;

    public EncryptedTokenFile(string path, IKeyStorage keyStorage)
    {
        _path = path;
        _keyStorage = keyStorage;
    }

    /// <summary>Returns the decrypted cache, or null when no cache file exists yet.</summary>
    public byte[]? Read()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var encryptedData = File.ReadAllBytes(_path);
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[encryptedData.Length - NonceSize - TagSize];
            Array.Copy(encryptedData, nonce, NonceSize);
            Array.Copy(encryptedData, NonceSize, tag, 0, TagSize);
            Array.Copy(encryptedData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            using var aes = new AesGcm(GetOrCreateKey(), TagSize);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
    }

    /// <summary>Encrypts <paramref name="plaintext"/> with a fresh nonce and replaces the cache file.</summary>
    public void Write(byte[] plaintext)
    {
        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var aes = new AesGcm(GetOrCreateKey(), TagSize);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            var encryptedData = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Array.Copy(nonce, encryptedData, nonce.Length);
            Array.Copy(tag, 0, encryptedData, nonce.Length, tag.Length);
            Array.Copy(ciphertext, 0, encryptedData, nonce.Length + tag.Length, ciphertext.Length);
            File.WriteAllBytes(_path, encryptedData);
        }
    }

    private byte[] GetOrCreateKey()
    {
        if (_cachedKey != null)
        {
            return _cachedKey;
        }

        try
        {
            _cachedKey = _keyStorage.GetKey();
            return _cachedKey;
        }
        catch
        {
            var key = new byte[KeySize];
            RandomNumberGenerator.Fill(key);
            _keyStorage.SaveKey(key);
            _cachedKey = key;
            return key;
        }
    }
}
