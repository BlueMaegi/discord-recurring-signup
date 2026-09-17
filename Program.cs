using System;
using DSharpPlus;
using DSharpPlus.Entities;

namespace RecurringSignup;

public class Program
{
    static async Task Main(string[] args)
    {
        var config = new DiscordConfiguration()
        {
            Token = "",
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        };

        var client = new DiscordClient(config);
        var status = new DiscordActivity("with fire", ActivityType.Playing);

        client.MessageCreated += async (s, e) => 
        {
            if (e.Message.Content.ToLower().StartsWith("ping"))
            {
                await e.Message.RespondAsync("pong!");
            }
        };

        await client.ConnectAsync(status, UserStatus.Online);
        await Task.Delay(-1);
    }
}
