namespace FanFundingBot.App.Auth;

public sealed record TelegramAuthRequest(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    long AuthDate,
    string? Hash)
{
    public IReadOnlyDictionary<string, string?> ToFields()
    {
        return new Dictionary<string, string?>
        {
            ["id"] = Id.ToString(),
            ["first_name"] = FirstName,
            ["last_name"] = LastName,
            ["username"] = Username,
            ["photo_url"] = PhotoUrl,
            ["auth_date"] = AuthDate.ToString(),
            ["hash"] = Hash
        };
    }
}
