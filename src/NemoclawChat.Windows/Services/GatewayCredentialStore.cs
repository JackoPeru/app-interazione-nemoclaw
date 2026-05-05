using Windows.Security.Credentials;

namespace NemoclawChat_Windows.Services;

public static class GatewayCredentialStore
{
    private const string Resource = "ChatClaw.OpenClawGateway";
    private const string UserName = "operator";

    public static bool HasSecret()
    {
        return !string.IsNullOrWhiteSpace(LoadSecret());
    }

    public static string LoadSecret()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
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
            var vault = new PasswordVault();
            vault.Remove(vault.Retrieve(Resource, UserName));
        }
        catch
        {
            // Nothing to delete or credential locker unavailable.
        }
    }
}
