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
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseMySql(connectionString, serverVersion));
        var serviceProvider = services.BuildServiceProvider();
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var signupHandler = new SignupHandler(dbContextFactory);
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
                    var channel = e.Message.Channel.Id;
                    var text = signupHandler.Get(1)?.Name ?? "This shouldn't happen";
                    await e.Message.RespondAsync(text);
                }
            }).HandleComponentInteractionCreated(async (s, e) =>
            {
                if (e.Interaction.Type == DiscordInteractionType.Component 
                    && e.Interaction.Data.ComponentType == DiscordComponentType.Button)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource);

                    var buttonId = e.Interaction.Data.CustomId;
                    if (buttonId.EndsWith("-join"))
                    {
                        var response = signupHandler.SetUser(e.User, buttonId);
                        await e.Interaction.CreateFollowupMessageAsync(response);
                        signupHandler.UpdatePreviousReminders(e.Message);
                    }
                    if (buttonId.EndsWith("-leave"))
                    {
                        var response = signupHandler.SetUser(e.User, buttonId, true);
                        await e.Interaction.CreateFollowupMessageAsync(response);
                        signupHandler.UpdatePreviousReminders(e.Message);
                    }
                }
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

        var interval = configuration.GetValue<int>("PollIntervalSeconds");
        var handler = new SignupHandler(dbContextFactory);
        _ = Task.Run(() => { handler.StartPoll(client, interval); });
        await Task.CompletedTask;

        await Task.Delay(-1);
    }
}
