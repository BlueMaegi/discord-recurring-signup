using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RecurringSignup.Models;

namespace RecurringSignup.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Event> Events {get; set;}
    public DbSet<User> Users {get; set;}


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //Use reflection to find all the DB maps in the "Maps" namespace and load them up
        var mapList = Assembly.GetExecutingAssembly().GetTypes()
            .Where(x => x.IsClass
            && x.Namespace == "RecurringSignup.Maps"
            && x.GetInterfaces().Length > 0
            && !x.ContainsGenericParameters);

        foreach (var m in mapList)
        {
            dynamic mapInstance = Activator.CreateInstance(m) ?? new object();
            modelBuilder.ApplyConfiguration(mapInstance);
        }
    }
}
