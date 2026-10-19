using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Todo.CLI.Auth;
using Xunit;

namespace Todo.CLI.Tests.Auth;

/// <summary>Covers NFR-2: the token cache is encrypted at rest and its key is kept out of the cache file.</summary>
public sealed class EncryptedTokenFileTests : IDisposable
{
    private const string Secret = "eyJ0b2tlbiI6InN1cGVyLXNlY3JldC1yZWZyZXNoLXRva2VuIn0";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "todo-cli-tests-" + Guid.NewGuid().ToString("N"));
    private string CachePath => Path.Combine(_directory, "token.cache");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Read_NoCacheFile_ReturnsNull()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());

        Assert.Null(store.Read());
    }

    [Fact]
    public void WriteThenRead_RoundTripsThePlaintext()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());
        var plaintext = Encoding.UTF8.GetBytes("{\"AccessToken\":{\"x\":\"" + Secret + "\"}}");

        store.Write(plaintext);

        Assert.Equal(plaintext, store.Read());
    }

    [Fact]
    public void Write_CreatesMissingDirectory()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());

        store.Write([1, 2, 3]);

        Assert.True(File.Exists(CachePath));
    }

    [Fact]
    public void Write_FileOnDiskDoesNotContainThePlaintextToken()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());

        store.Write(Encoding.UTF8.GetBytes(Secret));

        var onDisk = File.ReadAllBytes(CachePath);
        Assert.DoesNotContain(Secret, Encoding.Latin1.GetString(onDisk), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_FileOnDiskDoesNotContainTheKey()
    {
        var keys = new FakeKeyStorage();
        var store = new EncryptedTokenFile(CachePath, keys);

        store.Write(Encoding.UTF8.GetBytes(Secret));

        var onDisk = Encoding.Latin1.GetString(File.ReadAllBytes(CachePath));
        Assert.DoesNotContain(Encoding.Latin1.GetString(keys.Key!), onDisk, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_SamePlaintextTwice_UsesAFreshNonce()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());
        var plaintext = Encoding.UTF8.GetBytes(Secret);

        store.Write(plaintext);
        var first = File.ReadAllBytes(CachePath);
        store.Write(plaintext);
        var second = File.ReadAllBytes(CachePath);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Read_WithADifferentKey_TreatsTheCacheAsEmpty()
    {
        new EncryptedTokenFile(CachePath, new FakeKeyStorage()).Write(Encoding.UTF8.GetBytes(Secret));
        var otherKey = new FakeKeyStorage { Key = RandomNumberGenerator.GetBytes(32) };

        Assert.Null(new EncryptedTokenFile(CachePath, otherKey).Read());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(28)]
    public void Read_TamperedFile_TreatsTheCacheAsEmptyInsteadOfDecrypting(int byteIndex)
    {
        var keys = new FakeKeyStorage();
        new EncryptedTokenFile(CachePath, keys).Write(Encoding.UTF8.GetBytes(Secret));
        var bytes = File.ReadAllBytes(CachePath);
        bytes[byteIndex] ^= 0xFF;
        File.WriteAllBytes(CachePath, bytes);

        Assert.Null(new EncryptedTokenFile(CachePath, keys).Read());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(27)]
    public void Read_EmptyOrTruncatedFile_TreatsTheCacheAsEmpty(int length)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(CachePath, new byte[length]);

        Assert.Null(new EncryptedTokenFile(CachePath, new FakeKeyStorage { Key = RandomNumberGenerator.GetBytes(32) }).Read());
    }

    [Fact]
    public void Write_AfterACorruptCache_ReplacesItWithAReadableOne()
    {
        var keys = new FakeKeyStorage();
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(CachePath, [1, 2, 3]);
        var store = new EncryptedTokenFile(CachePath, keys);
        Assert.Null(store.Read());

        store.Write(Encoding.UTF8.GetBytes(Secret));

        Assert.Equal(Encoding.UTF8.GetBytes(Secret), store.Read());
    }

    [Fact]
    public void Read_KeyStoreFails_PropagatesTheFailureInsteadOfTreatingItAsNoKey()
    {
        var keys = new FakeKeyStorage();
        new EncryptedTokenFile(CachePath, keys).Write(Encoding.UTF8.GetBytes(Secret));
        var before = File.ReadAllBytes(CachePath);
        var failing = new FakeKeyStorage { GetFailure = new IOException("key store unavailable") };

        Assert.Throws<IOException>(() => new EncryptedTokenFile(CachePath, failing).Read());
        Assert.Equal(0, failing.SaveCount);
        Assert.Equal(before, File.ReadAllBytes(CachePath));
    }

    [Fact]
    public void Write_KeyStoreFails_DoesNotReplaceTheStoredKeyOrTheCache()
    {
        var keys = new FakeKeyStorage();
        new EncryptedTokenFile(CachePath, keys).Write(Encoding.UTF8.GetBytes(Secret));
        var before = File.ReadAllBytes(CachePath);
        var failing = new FakeKeyStorage { GetFailure = new IOException("key store unavailable") };

        Assert.Throws<IOException>(() => new EncryptedTokenFile(CachePath, failing).Write([9, 9, 9]));
        Assert.Equal(0, failing.SaveCount);
        Assert.Equal(before, File.ReadAllBytes(CachePath));
        Assert.Equal(Encoding.UTF8.GetBytes(Secret), new EncryptedTokenFile(CachePath, keys).Read());
    }

    [Fact]
    public void Write_OnUnix_CreatesTheCacheOwnerOnlyInAnOwnerOnlyDirectory()
    {
        if (OperatingSystem.IsWindows())
            return;

        new EncryptedTokenFile(CachePath, new FakeKeyStorage()).Write(Encoding.UTF8.GetBytes(Secret));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(CachePath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
    }

    [Fact]
    public void Write_OnUnix_TightensAnExistingPermissiveCacheAndDirectory()
    {
        if (OperatingSystem.IsWindows())
            return;

        var keys = new FakeKeyStorage();
        var store = new EncryptedTokenFile(CachePath, keys);
        store.Write([1, 2, 3]);
        File.SetUnixFileMode(_directory, (UnixFileMode)Convert.ToInt32("775", 8));
        File.SetUnixFileMode(CachePath, (UnixFileMode)Convert.ToInt32("664", 8));

        store.Write(Encoding.UTF8.GetBytes(Secret));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(CachePath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
    }

    [Fact]
    public void Write_LeavesNoTemporaryFileBehind()
    {
        var store = new EncryptedTokenFile(CachePath, new FakeKeyStorage());

        store.Write([1, 2, 3]);
        store.Write([4, 5, 6]);

        Assert.Equal([CachePath], Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void Write_NoStoredKey_CreatesAndSavesA256BitKeyThatLaterInstancesReuse()
    {
        var keys = new FakeKeyStorage();
        new EncryptedTokenFile(CachePath, keys).Write(Encoding.UTF8.GetBytes(Secret));

        Assert.Equal(1, keys.SaveCount);
        Assert.Equal(32, keys.Key!.Length);
        Assert.Equal(Encoding.UTF8.GetBytes(Secret), new EncryptedTokenFile(CachePath, keys).Read());
        Assert.Equal(1, keys.SaveCount);
    }

    [Fact]
    public void Read_ExistingKey_IsFetchedOnceAndNeverResaved()
    {
        var keys = new FakeKeyStorage { Key = RandomNumberGenerator.GetBytes(32) };
        var store = new EncryptedTokenFile(CachePath, keys);

        store.Write([1, 2, 3]);
        store.Read();
        store.Write([4, 5, 6]);
        store.Read();

        Assert.Equal(1, keys.GetCount);
        Assert.Equal(0, keys.SaveCount);
    }

    private sealed class FakeKeyStorage : IKeyStorage
    {
        public byte[]? Key { get; set; }

        /// <summary>When set, GetKey throws it to simulate a key store failing for a reason other than "no key".</summary>
        public Exception? GetFailure { get; set; }

        public int GetCount { get; private set; }

        public int SaveCount { get; private set; }

        public byte[] GetKey()
        {
            GetCount++;
            if (GetFailure is not null)
                throw GetFailure;
            return Key ?? throw new KeyNotFoundException("No key stored.");
        }

        public void SaveKey(byte[] key)
        {
            SaveCount++;
            Key = key;
        }
    }
}
