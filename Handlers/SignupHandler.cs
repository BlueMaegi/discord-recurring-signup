using DSharpPlus;
using RecurringSignup.Data;
using RecurringSignup.Models;

namespace RecurringSignup.Handlers;

public class SignupHandler
{
    private readonly ApplicationDbContext dbContext;
    public SignupHandler(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Event? Get(int id)
    {
        return dbContext.Events.FirstOrDefault(x => x.Id == id);
    }

    public async Task StartPoll(DiscordClient client, int interval)
    {
        var nextRun = DateTime.Now.AddSeconds(interval);
        while(true)
        {
            if (DateTime.Now > nextRun)
            {
                nextRun.AddSeconds(interval);

                var events = dbContext.Events.ToList();
                foreach(var e in events)
                {
                    //TODO: check if the event is due for a Signup announcement or a Reminder
                    var channel = await client.GetChannelAsync(e.ChannelId);
                    if (channel != null)
                    {
                        //TODO; Check for an existing thread, if not make one
                        await channel.SendMessageAsync("Beep Boop, reminder to sign up for: " + e.Name);
                        //TODO: Signup buttons
                    }
                }
            }
        }
    }

    //TODO: Signup Button Handler
    //TODO: Send a message that only the creator can see?
}
