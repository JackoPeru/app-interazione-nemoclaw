using Windows.Security.Credentials;

namespace NemoclawChat_Windows.Services;

public static class GatewayCredentialStore
{
    private const string Resource = "HermesHub.ApiKey";
    private const string LegacyResource = "ChatClaw.OpenClawGateway";
    private const string UserName = "hermes";
    private const string LegacyUserName = "operator";

    // PasswordVault e' wrapper COM thread-safe: cachiamo singleton invece di allocare per call.
    private static readonly Lazy<PasswordVault> SharedVault = new(() => new PasswordVault());

    public static bool HasSecret()
    {
        return !string.IsNullOrWhiteSpace(LoadSecret());
    }

    public static string LoadSecret()
    {
        try
        {
            var vault = SharedVault.Value;
            var credential = vault.Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // ERROR_NOT_FOUND (0x80070490) = credential non presente: fallback legacy.
            // Altri COM error (Locker bloccato, profilo mancante): log + fallback.
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] vault.Retrieve COM error 0x{ex.HResult:X}: {ex.Message}");
            return LoadLegacySecret();
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] crypto error: {ex.Message}");
            return LoadLegacySecret();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] unexpected error: {ex.GetType().Name}: {ex.Message}");
            return LoadLegacySecret();
        }
    }

    public static void SaveSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        DeleteSecret();
        var vault = new PasswordVault();
        vault.Add(new PasswordCredential(Resource, UserName, secret.Trim()));
    }

    public static void DeleteSecret()
    {
        try
        {
            var vault = SharedVault.Value;
            vault.Remove(vault.Retrieve(Resource, UserName));
        }
        catch
        {
            // Nothing to delete or credential locker unavailable.
        }
    }

    private static string LoadLegacySecret()
    {
        try
        {
            var vault = SharedVault.Value;
            var credential = vault.Retrieve(LegacyResource, LegacyUserName);
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
