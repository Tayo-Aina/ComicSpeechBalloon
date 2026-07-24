using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ComicSpeechBalloon;

/// <summary>
/// Securely stores the DeepSeek API key in the user's AppData folder,
/// protected by Windows Data Protection API (DPAPI) so only the current
/// Windows user account can decrypt it.
/// Never touches source code or the project directory.
/// </summary>
public static class ApiKeyStore
{
    private static readonly string StorageDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ComicSpeechBalloon");

    private static readonly string KeyFile = Path.Combine(StorageDir, ".apikey");

    /// <summary>
    /// Returns the stored API key, or null if none has been saved.
    /// Also checks the DEEPSEEK_API_KEY environment variable as a fallback.
    /// </summary>
    public static string? LoadKey()
    {
        // Priority 1: environment variable (for power users / deployment)
        var envKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
            return envKey.Trim();

        // Priority 2: DPAPI-protected file in AppData
        try
        {
            if (!File.Exists(KeyFile))
                return null;

            var encrypted = File.ReadAllBytes(KeyFile);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var key = Encoding.UTF8.GetString(decrypted).Trim();
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Persists the API key to the protected AppData file.
    /// Pass null or empty string to delete the stored key.
    /// </summary>
    public static void SaveKey(string? key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (File.Exists(KeyFile))
                    File.Delete(KeyFile);
                return;
            }

            Directory.CreateDirectory(StorageDir);

            var plain = Encoding.UTF8.GetBytes(key.Trim());
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, encrypted);

            // Hide the file from casual browsing
            File.SetAttributes(KeyFile, File.GetAttributes(KeyFile) | FileAttributes.Hidden);
        }
        catch
        {
            // Silently fail — the app still works with built-in phrases
        }
    }

    /// <summary>
    /// Returns true if a key has been configured.
    /// </summary>
    public static bool HasKey => LoadKey() != null;

    /// <summary>
    /// Delete the stored key file (for uninstall / cleanup).
    /// </summary>
    public static void DeleteKey()
    {
        try
        {
            if (File.Exists(KeyFile))
                File.Delete(KeyFile);
        }
        catch { }
    }
}
