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
        return false;
    }

    public static string LoadSecret()
    {
        return string.Empty;
    }

    public static void SaveSecret(string secret)
    {
        DeleteSecret();
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
}
