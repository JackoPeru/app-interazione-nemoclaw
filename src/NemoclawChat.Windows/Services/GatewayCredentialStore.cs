using Windows.Security.Credentials;

namespace NemoclawChat_Windows.Services;

public static class GatewayCredentialStore
{
    private const string Resource = "HermesHub.ApiKey";
    private const string LegacyResource = "ChatClaw.OpenClawGateway";
    private const string UserName = "hermes";
    private const string LegacyUserName = "operator";
    public const string DefaultApiKey = "hermes-hub";

    // PasswordVault e' wrapper COM thread-safe: cachiamo singleton invece di allocare per call.
    private static readonly Lazy<PasswordVault> SharedVault = new(() => new PasswordVault());

    public static bool HasSecret()
    {
        return TryLoadSecret(Resource, UserName, out _) ||
               TryLoadSecret(LegacyResource, LegacyUserName, out _);
    }

    public static string LoadSecret()
    {
        if (TryLoadSecret(Resource, UserName, out var secret))
        {
            return secret;
        }

        if (TryLoadSecret(LegacyResource, LegacyUserName, out var legacySecret))
        {
            SaveSecret(legacySecret);
            return legacySecret;
        }

        return DefaultApiKey;
    }

    public static void SaveSecret(string secret)
    {
        DeleteSecret();
        var normalized = string.IsNullOrWhiteSpace(secret) ? DefaultApiKey : secret.Trim();
        try
        {
            SharedVault.Value.Add(new PasswordCredential(Resource, UserName, normalized));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] SaveSecret: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void DeleteSecret()
    {
        RemoveSecret(Resource, UserName);
        RemoveSecret(LegacyResource, LegacyUserName);
    }

    private static void RemoveSecret(string resource, string userName)
    {
        try
        {
            var vault = SharedVault.Value;
            vault.Remove(vault.Retrieve(resource, userName));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] DeleteSecret: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryLoadSecret(string resource, string userName, out string secret)
    {
        secret = string.Empty;
        try
        {
            var credential = SharedVault.Value.Retrieve(resource, userName);
            credential.RetrievePassword();
            secret = credential.Password ?? string.Empty;
            return !string.IsNullOrWhiteSpace(secret);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GatewayCredentialStore] LoadSecret: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
