using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Microsoft.Identity.Client;
using XboxAuthNet.Game;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;
using XboxAuthNet.Game.Authenticators;
using XboxAuthNet.Game.XboxAuth;
using System.Text.Json.Nodes;

namespace Ttro.Launcher.Core;

public sealed class AuthService(string root, string settingsPath)
{
    private JELoginHandler? handler;
    private MsalOAuthBuilder? oauth;
    private IAuthenticationProvider? xbox;
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
    public bool Configured => Guid.TryParse(ClientId, out var id) && id != Guid.Empty;
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
        oauth = new MsalOAuthBuilder(app); xbox = new BasicXboxProvider(JELoginHandler.RelyingParty);
        handler = new JELoginHandlerBuilder()
            .WithOAuthProvider(new MsalCodeFlowProvider(oauth))
            .WithXboxAuthProvider(xbox)
            .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
            .Build();
        return handler;
    }
    public Task<MSession> LoginAsync(CancellationToken ct) => AuthenticateAsync(true, ct);
    public Task<MSession> RefreshAsync(CancellationToken ct) => AuthenticateAsync(false, ct);
    private async Task<MSession> AuthenticateAsync(bool interactive, CancellationToken ct)
    {
        var h = await HandlerAsync();
        var authenticator = interactive ? h.CreateAuthenticatorWithNewAccount(ct) : h.CreateAuthenticatorWithDefaultAccount(ct);
        authenticator.AddAuthenticatorWithoutValidator(interactive ? oauth!.SystemBrowser() : oauth!.Silent());
        authenticator.AddAuthenticatorWithoutValidator(xbox!.Authenticate());
        // The library's convenience login checks a JE profile but does not enable
        // the separate entitlement checker. Require both on every Login and PLAY.
        authenticator.AddForceJEAuthenticator(builder => builder.WithGameOwnershipChecker().Build());
        Session = null; var verified = await authenticator.ExecuteForLauncherAsync(); ct.ThrowIfCancellationRequested();
        Session = verified; return verified;
    }
    public async Task SignOutAsync() { if (handler is not null) await handler.Signout(); Session = null; }
}
