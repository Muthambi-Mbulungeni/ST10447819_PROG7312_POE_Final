namespace SmartX.Api.Services;

using System.Security.Cryptography;

// Persists uploaded files as AES-256-CBC encrypted blobs.
// The IV is generated fresh per file and prepended to the cipher stream
// so decryption is self-contained without a separate metadata lookup.
// Key material lives beside the file. In production this belongs in a vault.
public sealed class FileService
{
    private readonly string _baseDir;

    public FileService(string baseDir)
    {
        if (string.IsNullOrWhiteSpace(baseDir))
            throw new ArgumentException("Base directory cannot be empty.", nameof(baseDir));

        _baseDir = baseDir;
        Directory.CreateDirectory(_baseDir);
    }

    public async Task<string> SaveEncryptedAsync(string deviceId, string originalName, Stream content)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID required.", nameof(deviceId));

        if (content is null) throw new ArgumentNullException(nameof(content));

        string deviceDir = Path.Combine(_baseDir, Sanitize(deviceId));
        Directory.CreateDirectory(deviceDir);

        string baseName  = Path.GetFileNameWithoutExtension(originalName);
        string destPath  = Path.Combine(deviceDir, $"{baseName}.enc");
        string keyPath   = Path.Combine(deviceDir, $"{baseName}.key");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();

        // Key file layout: [32 bytes key][16 bytes IV].
        byte[] keyMaterial = new byte[aes.Key.Length + aes.IV.Length];
        Buffer.BlockCopy(aes.Key, 0, keyMaterial, 0, aes.Key.Length);
        Buffer.BlockCopy(aes.IV,  0, keyMaterial, aes.Key.Length, aes.IV.Length);
        await File.WriteAllBytesAsync(keyPath, keyMaterial);

        await using var outFile = File.Create(destPath);
        await using var cs      = new CryptoStream(outFile, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await content.CopyToAsync(cs);

        return destPath;
    }

    // Strip path separators and control characters from IDs before using them as directory names.
    private static string Sanitize(string id) =>
        string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
