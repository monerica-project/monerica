namespace DirectoryManager.Web.Services.Interfaces
{
    public interface IPgpService
    {
        // Return a stable fingerprint from an ASCII-armored public key or null if invalid
        string? GetFingerprint(string armoredPublicKey);

        // Encrypts a plain text message to the provided armored public key and returns ASCII armored ciphertext
        string EncryptTo(string armoredPublicKey, string message);
    }
}
