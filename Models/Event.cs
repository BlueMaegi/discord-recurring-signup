namespace RecurringSignup.Models;

public class Event
{
    public int Id { get; set; }

    public int CreatedById { get; set; }
    public ulong ChannelId { get; set; }
    public string? Name { get; set; }
    public DateTime Date { get; set; }
    public DateTime DateCreated { get; set; }
    public int? Recurrence { get; set; } // in seconds
    public int SignupTime { get; set; } // in seconds
    public int? ReminderTime { get; set; } // in seconds
    public int? RequiredAttendees { get; set; }
    public int? OptionalAttendees { get; set; }

    public List<User>? Attendees { get; set; }
}

public enum Recurrence
{
    Hours,
    Days,
    Weeks,
    Months
}
