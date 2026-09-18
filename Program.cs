using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Commands.Processors.SlashCommands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecurringSignup.Handlers;
using RecurringSignup.Data;
using DSharpPlus.Commands;

namespace RecurringSignup;

public class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("AppDbContext");
        var serverVersion = ServerVersion.AutoDetect(connectionString); 
        services.AddDbContext<ApplicationDbContext>(options => options.UseMySql(connectionString, serverVersion));
        var serviceProvider = services.BuildServiceProvider();
        var token = configuration.GetValue<string>("AppToken");

        var builder = DiscordClientBuilder.CreateDefault(
            token,
            DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
            services);

        builder.ConfigureEventHandlers
        (
            b => b.HandleMessageCreated(async (s, e) => 
            {
                if (e.Message.Content.ToLower().StartsWith("ping"))
                {
                    var text = "Pong!";
                    var channel = e.Message.Channel.Id;
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var handler = new SignupHandler(context);
                        text = handler.Get(1)?.Name ?? "This shouldn't happen";
                    }

                    await e.Message.RespondAsync(text);
                }
            }).HandleGuildDownloadCompleted(async (client, eventArgs) =>
            {
                var interval = configuration.GetValue<int>("PollIntervalSeconds");
                _ = Task.Run(() => {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var handler = new SignupHandler(context);
                        handler.StartPoll(client, interval);
                    }
                });
                await Task.CompletedTask;
            })
        );
        
        builder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
        {
            extension.AddCommands([typeof(CreateHandler)]);
            //TODO: delete event command
            //TODO: list attendees command (creator only?, hide past events by default)
            //TODO: recurring events

            var slashProcessor = new SlashCommandProcessor();
            extension.AddProcessor(slashProcessor);
        },
        new CommandsConfiguration(){DebugGuildId = 1092894245133754408 });

        builder.ConfigureEventHandlers(eventBuilder => 
        {
            eventBuilder.AddEventHandlers<CreateSubmitHandler>();
        });

        var client = builder.Build();
        await client.ConnectAsync();
        await Task.Delay(-1);
    }
}
