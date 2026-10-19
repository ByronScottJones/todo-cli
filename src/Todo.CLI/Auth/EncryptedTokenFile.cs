using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Todo.CLI.Auth;

/// <summary>
/// Stores the serialized MSAL token cache in a file encrypted with AES-GCM. The key lives in platform key storage,
/// never next to the file. Layout on disk: 12-byte nonce, 16-byte tag, then the ciphertext.
/// On Unix the file is owner-only (0600) in an owner-only directory (0700); the directory is assumed to belong to
/// the application.
/// </summary>
internal sealed class EncryptedTokenFile
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectory = OwnerReadWrite | UnixFileMode.UserExecute;

    private readonly string _path;
    private readonly IKeyStorage _keyStorage;
    private readonly object _lock = new();
    private byte[]? _cachedKey;

    public EncryptedTokenFile(string path, IKeyStorage keyStorage)
    {
        _path = path;
        _keyStorage = keyStorage;
    }

    /// <summary>
    /// Returns the decrypted cache, or null when there is nothing usable: no file yet, or a file that is too short,
    /// was tampered with, or was encrypted under a different key. Treating those as an empty cache makes the user
    /// sign in again instead of failing every command. Key store failures are not swallowed; they propagate.
    /// </summary>
    public byte[]? Read()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var encryptedData = File.ReadAllBytes(_path);
            if (encryptedData.Length < NonceSize + TagSize)
            {
                return null;
            }

            var key = GetOrCreateKey();
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[encryptedData.Length - NonceSize - TagSize];
            Array.Copy(encryptedData, nonce, NonceSize);
            Array.Copy(encryptedData, NonceSize, tag, 0, TagSize);
            Array.Copy(encryptedData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            using var aes = new AesGcm(key, TagSize);
            var plaintext = new byte[ciphertext.Length];
            try
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                return null;
            }

            return plaintext;
        }
    }

    /// <summary>Encrypts <paramref name="plaintext"/> with a fresh nonce and replaces the cache file.</summary>
    public void Write(byte[] plaintext)
    {
        lock (_lock)
        {
            var key = GetOrCreateKey();
            EnsureDirectory();

            using var aes = new AesGcm(key, TagSize);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            var encryptedData = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Array.Copy(nonce, encryptedData, nonce.Length);
            Array.Copy(tag, 0, encryptedData, nonce.Length, tag.Length);
            Array.Copy(ciphertext, 0, encryptedData, nonce.Length + tag.Length, ciphertext.Length);
            WriteAtomically(encryptedData);
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, OwnerOnlyDirectory);
        // CreateDirectory leaves an existing directory (and applies the umask to a new one), so set it explicitly.
        File.SetUnixFileMode(directory, OwnerOnlyDirectory);
    }

    /// <summary>
    /// Writes to a temporary file that is owner-only from creation, then moves it over the cache, so the token data
    /// is never readable by others and a crash cannot leave a half-written cache.
    /// </summary>
    private void WriteAtomically(byte[] data)
    {
        var temp = _path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerReadWrite;
        }

        using (var stream = new FileStream(temp, options))
        {
            stream.Write(data);
        }

        if (!OperatingSystem.IsWindows())
        {
            // A leftover temp file from an earlier crash keeps its old mode; make sure it is owner-only.
            File.SetUnixFileMode(temp, OwnerReadWrite);
        }

        File.Move(temp, _path, overwrite: true);
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
        catch (KeyNotFoundException)
        {
            // Only a genuinely missing key creates a new one. Any other failure propagates: replacing the key
            // because the store was briefly unavailable would make the existing cache undecryptable.
            var key = new byte[KeySize];
            RandomNumberGenerator.Fill(key);
            _keyStorage.SaveKey(key);
            _cachedKey = key;
            return key;
        }
    }
}
