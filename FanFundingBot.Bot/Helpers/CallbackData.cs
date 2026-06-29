namespace FanFundingBot.Bot.Helpers;

public static class CallbackData
{
    public static (string Command, string[] Args) Parse(string data)
    {
        var parts = data.Split(':');
        return (parts[0], parts[1..]);
    }

    public static string FirstArg(string data) => Parse(data).Args[0];

    public static Guid GuidArg(string data, int index = 0) => Guid.Parse(Parse(data).Args[index]);

    public static int IntArg(string data, int index = 0) => int.Parse(Parse(data).Args[index]);

    public static string Build(string command, params object[] args)
        => string.Join(":", new[] { command }.Concat(args.Select(a => a.ToString())));
}
