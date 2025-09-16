using Google.Apis.Auth.OAuth2;

namespace Meetter.Providers.Google;

public static class GoogleOAuthConfig
{
    public static ClientSecrets GetClientSecrets() => new ClientSecrets
    {
        ClientId = GoogleSecrets.ClientId,
        ClientSecret = GoogleSecrets.ClientSecret
    };
}


