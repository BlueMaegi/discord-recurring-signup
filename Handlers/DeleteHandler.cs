using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using RecurringSignup.Data;

namespace RecurringSignup.Handlers;

public class DeleteHandler
{
    private readonly IServiceProvider serviceProvider;

    public DeleteHandler(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    // designates /delete_signup command
    [Command("delete_signup")]
    [AllowedProcessors<SlashCommandProcessor>]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        var message = new DiscordInteractionResponseBuilder()
            .EnableV2Components()
            .AsEphemeral();

        var modal = new DiscordModalBuilder
        {
            CustomId = "67890",
            Title = "Choose an Event to remove"
        };

        var eventOptions = new List<DiscordSelectComponentOption>();
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = dbContext.Users.FirstOrDefault(x => x.DiscordId == ctx.User.Id.ToString());
            var events = dbContext.Events
                .Where(x => x.Date > DateTime.UtcNow 
                    && (x.ChannelId == ctx.Channel.Id || x.ChannelId == ctx.Channel.ParentId)
                    && user != null && x.CreatedById == user.Id)
                .OrderBy(x => x.Date).ToList();

            foreach(var e in events)
            {
                var option = new DiscordSelectComponentOption($"{e.Name} ({e.Date.ToShortDateString()})", e.Id.ToString());
                eventOptions.Add(option);
            }

            if (user == null || !eventOptions.Any())
            {
                var error = "❌ **You don't have any events to manage.";
                message.AddTextDisplayComponent(new DiscordTextDisplayComponent(error));
                await ctx.RespondAsync(message);
                return;
                
            }
        }

        //TODO: warn about deleting recurring once we have them
        var eventDropdown = new DiscordSelectComponent("delete.Dropdown", "-- select --", eventOptions, required:true);
        modal.AddSelectMenu(eventDropdown, "");

        await ((SlashCommandContext)(ctx)).Interaction.CreateResponseAsync(
            DiscordInteractionResponseType.Modal, 
            modal
        );
    }
}
