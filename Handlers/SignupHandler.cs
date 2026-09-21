using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using RecurringSignup.Data;
using RecurringSignup.Models;

namespace RecurringSignup.Handlers;

public class SignupHandler
{
    private readonly IDbContextFactory<ApplicationDbContext> dbContextFactory;
    public SignupHandler(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory;
    }

    public Event? Get(int id)
    {
        Event result = null;
        using (var dbContext = dbContextFactory.CreateDbContext())
            result = dbContext.Events.FirstOrDefault(x => x.Id == id);
        return result;
    }

    public async Task StartPoll(DiscordClient client, int interval)
    {
        var nextRun = DateTime.Now.AddSeconds(interval);
        while(true)
        {
            if (DateTime.Now < nextRun) continue;

            nextRun = nextRun.AddSeconds(interval);
            var now = DateTime.Now;
            
            using (var dbContext = dbContextFactory.CreateDbContext())
            {
                var events = dbContext.Events
                    .Include(x => x.ParentEvent)
                    .Include(x => x.Attendees)
                    .Where(x => 
                        x.Date > now
                        && ((!x.SignupAlerted && x.Date.AddSeconds(x.SignupTime * -1) < now)
                        || (!x.ReminderAlerted ))//&& x.ReminderTime != null && x.Date.AddSeconds(x.ReminderTime.Value * -1) < now))
                    ).ToList();
                    
                foreach(var e in events)
                {
                    var threadId = e.ThreadId;
                    var channel = await client.GetChannelAsync(e.ChannelId);
                    if (channel != null)
                    {
                        var message = MakeSignup(e, e.SignupAlerted);
                        var thread = channel.Threads.FirstOrDefault(x => x.Id == threadId);
                        if (thread == null)
                        {
                            var triggerMessage = await new DiscordMessageBuilder()
                                .WithContent($"Creating a thread for {e.Name} signups...")
                                .SendAsync(channel);
                            thread = await channel.CreateThreadAsync(triggerMessage, $"Signups for {e.Name}", DiscordAutoArchiveDuration.Week);
                            e.ThreadId = thread.Id;
                        }

                        await message.SendAsync(thread);
                    }
                    
                    if (!e.SignupAlerted) e.SignupAlerted = true;
                    //else if (!e.ReminderAlerted) e.ReminderAlerted = true;
                    dbContext.SaveChanges();
                }
            }
        }
    }

    public DiscordMessageBuilder MakeSignup(Event e, bool reminder = false)
    {
        var cappedRequired = Math.Min(e.Attendees.Count, (e.RequiredAttendees ?? 0));
        var cappedOptional = Math.Max(0, Math.Min(e.Attendees.Count - (e.RequiredAttendees ?? 0), (e.OptionalAttendees ?? 0)));
        var text1 = reminder ? new DiscordTextDisplayComponent($"*** Reminder that {e.Name} is happening soon! ***")
            : new DiscordTextDisplayComponent($"*** Signups are now open for {e.Name}! ***");
        var text2 = new DiscordTextDisplayComponent($"{e.Date.ToLongDateString()} at {e.Date.ToShortTimeString()}");
        var text3 = new DiscordTextDisplayComponent($"There are {cappedRequired} of {e.RequiredAttendees} required slot(s) filled:");
        var text4 = new DiscordTextDisplayComponent($"There are {cappedOptional} of {e.OptionalAttendees} optional slot(s) filled:");
        var text5Text = ""; 
        for (var i = 0; i < (e.RequiredAttendees ?? 0) + (e.OptionalAttendees ?? 0); i++)
        {
            if (i < e.Attendees.Count) text5Text += "\n - " + e.Attendees[i].Username;
            else text5Text += "\n - [empty]";
            if (i < e.RequiredAttendees) text5Text += " ʳᵉᵠᵘⁱʳᵉᵈ";
        }
        var text5 = new DiscordTextDisplayComponent(text5Text);
        var separator = new DiscordSeparatorComponent(true);
        var joinButton = new DiscordButtonComponent(DiscordButtonStyle.Primary, e.Id.ToString()+"-join", "Sign up!");
        var leaveButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, e.Id.ToString()+"-leave", "Leave list");

        var builder = new DiscordMessageBuilder().EnableV2Components();
        builder.AddTextDisplayComponent(text1);
        builder.AddTextDisplayComponent(text2);
        builder.AddSeparatorComponent(separator);
        if (e.RequiredAttendees > 0) builder.AddTextDisplayComponent(text3);
        if (e.OptionalAttendees > 0) builder.AddTextDisplayComponent(text4);
        builder.AddTextDisplayComponent(text5);
        builder.AddSeparatorComponent(separator);
        builder.AddActionRowComponent(joinButton, leaveButton);

        return builder;
    }

    public DiscordFollowupMessageBuilder SetUser(DiscordUser discordUser, string? buttonId, bool remove = false)
    {
        var builder = new DiscordFollowupMessageBuilder().AsEphemeral();
        var message = "";

        buttonId = buttonId?.Split("-").FirstOrDefault();
        if (!int.TryParse(buttonId, out int eventId))
            return builder;

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            var signupEvent = dbContext.Events.Include(x => x.Attendees).FirstOrDefault(x => x.Id == eventId);
            var user = dbContext.Users.FirstOrDefault(x => x.DiscordId == discordUser.Id.ToString());
            if (user == null)
            {
                user = new User() {Username = discordUser.Username, DiscordId = discordUser.Id.ToString()};
                dbContext.Users.Add(user);
                dbContext.SaveChanges();
            }

            if (signupEvent != null)
            {
                if (signupEvent.Attendees.Any(x => x.Id == user.Id))
                {
                    if (remove)
                    {
                        signupEvent.Attendees.Remove(user);
                        dbContext.SaveChanges();
                        message = "You have been successfully removed from the list.";
                    }
                    else
                        message = "Silly goose, you've already signed up for this event!";
                }
                else if (!remove && signupEvent.Attendees.Count >= signupEvent.RequiredAttendees + signupEvent.OptionalAttendees)
                    message = "Sorry, this event is already full.";
                else if (!remove)
                {
                    signupEvent.Attendees.Add(user);
                    dbContext.SaveChanges();
                    message = "Your name has been added!";
                }
            }
        }

        builder.WithContent(message);
        return builder;
    }

    public async Task UpdatePreviousReminders(DiscordMessage message)
    {
        var thread = message.Channel;
        if (thread == null) return;
        
        var messages = await thread.GetMessagesAsync().ToListAsync();
        foreach(var m in messages)
        {
            var buttons = m.FilterComponents<DiscordButtonComponent>();
            if (!buttons.Any(x => x.CustomId.EndsWith("-join"))) continue;
            var buttonId = buttons.First(x => x.CustomId.EndsWith("-join")).CustomId.Split("-").FirstOrDefault();
            if (!int.TryParse(buttonId, out int eventId)) continue;
            
            var text = m.FilterComponents<DiscordTextDisplayComponent>();
            var isReminder = text.Any(x => x.Content.Contains("Reminder"));
            Event? originalEvent;
            using (var dbContext = dbContextFactory.CreateDbContext())
            {
                originalEvent = dbContext.Events.Include(x => x.Attendees).FirstOrDefault(x => x.Id == eventId);
            }
            if (originalEvent == null) continue;

            var freshContent = MakeSignup(originalEvent, isReminder);
            await m.ModifyAsync(freshContent);
        }
        return;
    }
}
