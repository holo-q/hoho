using Hoho.Core.Configuration;

namespace Hoho.Core.Authentication;

public sealed class AuthService
{
    private readonly HohoConfig _cfg;
    public AuthService(HohoConfig cfg) { _cfg = cfg; }

    public void LoginChatGpt(string sessionToken)
    {
        _cfg.SetChatGptSession(sessionToken).Save();
    }

    public void Logout()
    {
        _cfg.ClearAuth().Save();
    }

    public string WhoAmI()
    {
        return _cfg.AuthProvider switch
        {
            "chatgpt" when !string.IsNullOrWhiteSpace(_cfg.ChatGptSession) => "ChatGPT: signed in",
            _ => "Not signed in",
        };
    }
}

