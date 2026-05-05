namespace NemoclawChat_Windows.Services;

public sealed class AppSettings
{
    public string GatewayUrl { get; set; } = "https://openclaw.local:8443";
    public string GatewayWsUrl { get; set; } = "wss://openclaw.local:8443";
    public string AdminBridgeUrl { get; set; } = "https://openclaw.local:9443";
    public string Provider { get; set; } = "custom";
    public string InferenceEndpoint { get; set; } = "http://localhost:8000/v1";
    public string PreferredApi { get; set; } = "openai-completions";
    public string Model { get; set; } = "meta-llama/Llama-3.1-8B-Instruct";
    public string AccessMode { get; set; } = "VPN/LAN only";
    public bool DemoMode { get; set; } = true;
}
