using OpenClaw.Shared;

namespace OpenClaw.Connection;

/// <summary>
/// Wraps <see cref="OpenClawGatewayClient"/> behind <see cref="IGatewayClientLifecycle"/>.
/// Creates a real WebSocket-connected client instance.
/// </summary>
public sealed class GatewayClientFactory : IGatewayClientFactory
{
    public IGatewayClientLifecycle Create(
        string gatewayUrl,
        GatewayCredential credential,
        string identityPath,
        IOpenClawLogger logger) =>
        Create(gatewayUrl, credential, identityPath, logger, clientCapabilities: null);

    /// <summary>
    /// As above, declaring what this client can do in the connect handshake.
    ///
    /// Separate from the interface method on purpose: <see cref="IGatewayClientFactory"/>
    /// has mock implementations, and widening it would break every one of
    /// them for a capability most callers do not want. A caller that needs
    /// caps holds the concrete factory.
    ///
    /// "approvals" is the one that matters today — the gateway broadcasts
    /// exec approval requests only to clients that declare it (or that carry
    /// one of four known approval client ids). Declare it only when there is
    /// a surface that can actually answer.
    /// </summary>
    public IGatewayClientLifecycle Create(
        string gatewayUrl,
        GatewayCredential credential,
        string identityPath,
        IOpenClawLogger logger,
        IReadOnlyList<string>? clientCapabilities)
    {
        var client = new OpenClawGatewayClient(
            gatewayUrl,
            credential.Token,
            logger,
            tokenIsBootstrapToken: credential.IsBootstrapToken,
            bootstrapPairAsNode: false,
            identityPath: identityPath,
            ignoreStoredDeviceToken: credential.IsBootstrapToken,
            assistantMediaAuthToken: credential.InteractiveHttpToken,
            clientCapabilities: clientCapabilities);

        return new GatewayClientLifecycleAdapter(client);
    }
}

/// <summary>
/// Adapts <see cref="OpenClawGatewayClient"/> (which inherits from
/// <see cref="WebSocketClientBase"/>) to the <see cref="IGatewayClientLifecycle"/> interface.
/// </summary>
internal sealed class GatewayClientLifecycleAdapter : IGatewayClientLifecycle
{
    private readonly OpenClawGatewayClient _client;

    public GatewayClientLifecycleAdapter(OpenClawGatewayClient client)
    {
        _client = client;
        // Forward events from WebSocketClientBase
        _client.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        _client.AuthenticationFailed += (s, e) => AuthenticationFailed?.Invoke(this, e);
    }

    public OpenClawGatewayClient DataClient => _client;

    public event EventHandler<ConnectionStatus>? StatusChanged;
    public event EventHandler<string>? AuthenticationFailed;

    public Task ConnectAsync(CancellationToken ct) => _client.ConnectAsync();

    public void Dispose() => _client.Dispose();
}
