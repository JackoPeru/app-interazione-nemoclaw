namespace NemoclawChat_Windows.Services;

public sealed class AppSettings
{
    public string GatewayUrl { get; set; } = "http://hermes.local:8642/v1";
    public string GatewayWsUrl { get; set; } = string.Empty;
    public string AdminBridgeUrl { get; set; } = "http://hermes.local:8642";
    public string Provider { get; set; } = "hermes-agent";
    public string InferenceEndpoint { get; set; } = "http://hermes.local:8642/v1";
    public string PreferredApi { get; set; } = "openai-responses";
    public string Model { get; set; } = "hermes-agent";
    public string AccessMode { get; set; } = "Tailscale/LAN";
    public string VisualBlocksMode { get; set; } = "auto";
    public string VideoLibraryPath { get; set; } = string.Empty;
    public bool DemoMode { get; set; } = false;
}
