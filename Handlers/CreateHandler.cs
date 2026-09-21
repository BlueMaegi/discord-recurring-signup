using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using RecurringSignup.Data;
using RecurringSignup.Models;

namespace RecurringSignup.Handlers;

public class CreateHandler
{
    private readonly IServiceProvider serviceProvider;

    public CreateHandler(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    // designates /create_signup command
    [Command("create_signup")]
    [AllowedProcessors<SlashCommandProcessor>]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        /*var timespanOptions = new List<DiscordSelectComponentOption>();
        foreach(var x in Enum.GetValues<Recurrence>())
        {
            var option = new DiscordSelectComponentOption(x.ToString(), ((int)x).ToString());
            timespanOptions.Add(option);
        }*/

        var modal = new DiscordModalBuilder
        {
            CustomId = "12345",
            Title = "New Signup List"
        };

        var titleTextbox = new DiscordTextInputComponent("event.Name", min_length: 2, max_length: 500);
        modal.AddTextInput(titleTextbox, "Event Title");

        var dateTextbox = new DiscordTextInputComponent("event.Date", "01/30/2000 18:00", min_length:10, max_length:18);
        modal.AddTextInput(dateTextbox, "Date and Time", "24 hour clock");

        var signupAmountTextbox = new DiscordTextInputComponent("event.SignupTime", "5 Days", required:true);
        modal.AddTextInput(signupAmountTextbox, "Signups open in advance", "Hours, Days, or Weeks only");

        /*var recurCheckbox = new DiscordCheckboxComponent("event.Recurs", false);
        modal.AddCheckbox(recurCheckbox, "Is this a recurring event?");

        var recurAmountTextbox = new DiscordTextInputComponent("event.RecurTime", "5", required:false);
        modal.AddTextInput(recurAmountTextbox, "Recurs every ");

        var recurDropdown = new DiscordSelectComponent("event.RecurSpan", "Days/Weeks/...", timespanOptions, required:false);
        modal.AddSelectMenu(recurDropdown, "");
        */

        var requiredTextbox = new DiscordTextInputComponent("event.RequiredAttendees", "0", required:false);
        modal.AddTextInput(requiredTextbox, "Number of required attendees");

        var optionalTextbox = new DiscordTextInputComponent("event.OptionalAttendees", "0", required:false);
        modal.AddTextInput(optionalTextbox, "Number of optional attendees");


        await ((SlashCommandContext)(ctx)).Interaction.CreateResponseAsync(
            DiscordInteractionResponseType.Modal, 
            modal
        );
    }
}

public class CreateSubmitHandler : IEventHandler<ModalSubmittedEventArgs>
{
    private readonly IServiceProvider serviceProvider;

    public CreateSubmitHandler(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public async Task HandleEventAsync(DiscordClient sender, ModalSubmittedEventArgs e)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (e.Interaction.Data.CustomId == "12345")
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource);
                var user = dbContext.Users.FirstOrDefault(x => x.DiscordId == e.Interaction.User.Id.ToString());
                if (user == null)
                {
                    user = new User(){Username = e.Interaction.User.Username, DiscordId = e.Interaction.User.Id.ToString()};
                    dbContext.Users.Add(user);
                    dbContext.SaveChanges();
                }

                var eventTitle = ((TextInputModalSubmission) e.Values["event.Name"]).Value;
                var dateText = ((TextInputModalSubmission) e.Values["event.Date"]).Value;
                var signupText = ((TextInputModalSubmission) e.Values["event.SignupTime"]).Value;
                var signupTime = StringToTicks(signupText, out bool succeeded);
                var requiredText = ((TextInputModalSubmission) e.Values["event.RequiredAttendees"]).Value;
                var optionalText = ((TextInputModalSubmission) e.Values["event.OptionalAttendees"]).Value;
                //var doesRecur = ((CheckboxModalSubmission) e.Values["event.Recurs"]).Value;
                //var recurText = ((TextInputModalSubmission) e.Values["event.RecurTime"]).Value;

                if (!succeeded)
                {
                     await e.Interaction.CreateFollowupMessageAsync(
                        new DiscordFollowupMessageBuilder()
                            .WithContent("❌ **Invalid time span.** Please use a recognizable format like `5 Days` or `3 hours`. Months are not supported")
                            .AsEphemeral() 
                    );
                    return;
                }

                if (!DateTime.TryParse(dateText, out DateTime eventDate))
                {
                    await e.Interaction.CreateFollowupMessageAsync(
                        new DiscordFollowupMessageBuilder()
                            .WithContent("❌ **Invalid Date Format.** Please use a recognizable format like `MM/DD/YYYY` or `YYYY-MM-DD`.")
                            .AsEphemeral() 
                    );
                    return;
                }

                if (string.IsNullOrEmpty(signupText.ToNumeric()) || string.IsNullOrEmpty(signupText.ToNumeric()))
                {
                    await e.Interaction.CreateFollowupMessageAsync(
                        new DiscordFollowupMessageBuilder()
                            .WithContent("❌ **Invalid Number in a numeric field.**")
                            .AsEphemeral() 
                    );
                    return;
                }


                var newEvent = new Event()
                {
                    ChannelId = e.Interaction.ChannelId,
                    CreatedById = user.Id,
                    DateCreated = DateTime.UtcNow,
                    Name = eventTitle,
                    Date = eventDate.ToUniversalTime(),
                    RequiredAttendees = int.Parse(requiredText.ToNumeric()),
                    OptionalAttendees = int.Parse(optionalText.ToNumeric()),
                    SignupTime = signupTime,
                    //Recurrence = doesRecur ? EnumToTicks(recurSpanText, recurText) : null,
                    ReminderTime = 60 * 60 * 24
                };
                dbContext.Events.Add(newEvent);
                dbContext.SaveChanges();

                await e.Interaction.CreateFollowupMessageAsync(
                    new DiscordFollowupMessageBuilder()
                        .WithContent($"Successfully created signup list for: **{eventTitle}**!")
                        .AsEphemeral()
                );
            }
        }
    }

    public int EnumToTicks(string dropdownVal, string amount)
    {
        var increment = 60 * 60;
        var span = (Recurrence) int.Parse(dropdownVal);
        switch(span)
        {
            case Recurrence.Days:
                increment *= 24;
                break;
            case Recurrence.Weeks:
                increment += 24 * 7;
                break;
        }

        return increment * int.Parse(amount.ToNumeric());
    }

    public int StringToTicks(string text, out bool succeeded)
    {
        succeeded = true;
        var result = 0;

        var parts = text.Trim().Split(' ');
        if (parts.Length != 2)
        { 
            succeeded = false;
            return result;
        }
        if (parts[1].Length < 2)
        {
            succeeded = false;
            return result;
        }

        var amount = int.Parse(parts[0].ToNumeric());
        var spanText = parts[1].Trim().ToLower();
        var spanSuccess = Enum.TryParse<Recurrence>(spanText, true, out Recurrence span);
        if (!spanSuccess)
        {
            succeeded = false;
            return result;
        }

        var increment = 60 * 60;
        switch(span)
        {
            case Recurrence.Days:
                increment *= 24;
                break;
            case Recurrence.Weeks:
                increment += 24 * 7;
                break;
        }

        return increment * amount;
    }
}
