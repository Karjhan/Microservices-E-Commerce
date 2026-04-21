using Domain.Abstractions.Authentication;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.External.Google;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _config;

    public GoogleTokenValidator(IConfiguration config)
    {
        _config = config;
    }

    public async Task<GoogleUserInfo> ValidateAsync(string idToken)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _config["GoogleAuth:ClientId"] }
        });

        return new GoogleUserInfo(
            payload.Email,
            payload.Subject
        );
    }
}