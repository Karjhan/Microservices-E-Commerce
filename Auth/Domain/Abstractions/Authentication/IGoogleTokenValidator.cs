namespace Domain.Abstractions.Authentication;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(string idToken);
}

public record GoogleUserInfo(
    string Email,
    string ProviderId
);