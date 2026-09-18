using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecurringSignup.Models;

namespace RecurringSignup.Maps;

public class UserMap : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DiscordId).HasColumnName("DiscordId").HasMaxLength(500).IsUnicode(false);
        builder.Property(x => x.Username).HasColumnName("Username").HasMaxLength(100).IsUnicode(false);
    }
}
