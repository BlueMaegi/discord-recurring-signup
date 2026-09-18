using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecurringSignup.Models;

namespace RecurringSignup.Maps;

public class EventMap : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedById).HasColumnName("CreatedById");
        builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(500).IsUnicode(false);
        builder.Property(x => x.Date).HasColumnName("Date").HasColumnType("datetime");
        builder.Property(x => x.DateCreated).HasColumnName("DateCreated").HasColumnType("datetime");
        builder.Property(x => x.Recurrence).HasColumnName("Recurrence");
        builder.Property(x => x.SignupTime).HasColumnName("SignupTime");
        builder.Property(x => x.ReminderTime).HasColumnName("ReminderTime");
        builder.Property(x => x.RequiredAttendees).HasColumnName("RequiredAttendees");
        builder.Property(x => x.OptionalAttendees).HasColumnName("OptionalAttendees");

        builder.HasMany(x => x.Attendees).WithMany()
            .UsingEntity<Dictionary<string, object>>(
            "EventAttendees",
            r => r.HasOne<User>().WithMany()
                .HasForeignKey("UserId")
                .HasConstraintName("fk_user_attendees"),
            l => l.HasOne<Event>().WithMany()
                .HasForeignKey("EventId"),
            j =>
            {
                j.HasKey("EventId", "UserId");
                j.ToTable("EventAttendees");
                j.HasIndex(new[] { "EventId" }, "IX_Event_Attendees");
                j.HasIndex(new[] { "UserId" }, "IX_User_Attendee");
            }
        );
    }
}
