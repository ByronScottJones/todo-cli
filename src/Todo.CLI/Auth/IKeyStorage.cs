namespace Todo.CLI.Auth;

public interface IKeyStorage
{
    /// <summary>
    /// Returns the stored key. Throws <see cref="System.Collections.Generic.KeyNotFoundException"/> when no key has
    /// been saved yet; any other exception means the store failed and must not be treated as "no key".
    /// </summary>
    byte[] GetKey();

    void SaveKey(byte[] key);
}
