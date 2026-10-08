using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;
using System.Text.Json.Nodes;

namespace Ttro.Launcher.Core;

public sealed class AuthService(string root, string settingsPath)
{
    private JELoginHandler? handler;
    public MSession? Session { get; private set; }
    public string ClientId
    {
        get
        {
            var cid = Environment.GetEnvironmentVariable("TTRO_MICROSOFT_CLIENT_ID");
            if (string.IsNullOrEmpty(cid) && File.Exists(settingsPath)) cid = JsonNode.Parse(File.ReadAllText(settingsPath))?["microsoftClientId"]?.GetValue<string>();
            return cid ?? "";
        }
    }
    public bool Configured => Guid.TryParse(ClientId, out _);
    private async Task<JELoginHandler> HandlerAsync()
    {
        if (handler is not null) return handler;
        if (!Configured) throw new InvalidOperationException("Microsoft sign-in is not available in this development build. A registered Ttro Client application ID must be configured by the release maintainer.");
        // MSAL uses the system browser and native encrypted Windows cache.
        // Xbox/JE bearer tokens stay in memory; no plaintext cml_accounts.json.
        var app = MsalClientHelper.BuildApplication(ClientId);
        var authDir = Path.Combine(root, "auth"); Directory.CreateDirectory(authDir);
        var properties = new Microsoft.Identity.Client.Extensions.Msal.StorageCreationPropertiesBuilder("msal.cache", authDir).Build();
        await MsalClientHelper.RegisterCache(app, properties);
        handler = new JELoginHandlerBuilder()
            .WithOAuthProvider(new MsalCodeFlowProvider(app))
            .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
            .Build();
        return handler;
    }
    public async Task<MSession> LoginAsync(CancellationToken ct)
    {
        var h = await HandlerAsync(); Session = await h.AuthenticateInteractively(ct); return Session;
    }
    public async Task<MSession> RefreshAsync(CancellationToken ct)
    {
        var h = await HandlerAsync(); Session = await h.Authenticate(ct); return Session;
    }
    public async Task SignOutAsync() { if (handler is not null) await handler.Signout(); Session = null; }
}
