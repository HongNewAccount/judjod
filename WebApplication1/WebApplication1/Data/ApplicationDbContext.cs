using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApplication1.Models;

namespace WebApplication1.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<ReportAssignment> ReportAssignments { get; set; }
    public DbSet<ReportComment> ReportComments { get; set; }
    public DbSet<OrganizationEvent> OrganizationEvents { get; set; }
    public DbSet<EventAttendee> EventAttendees { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectOwner> ProjectOwners { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Configure Report entity
        modelBuilder.Entity<Report>()
            .HasKey(r => r.Id);
        modelBuilder.Entity<Report>()
            .HasOne(r => r.CreatedByUser)
            .WithMany(u => u.ReportsCreated)
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure ReportAssignment entity
        modelBuilder.Entity<ReportAssignment>()
            .HasKey(ra => ra.Id);
        modelBuilder.Entity<ReportAssignment>()
            .HasOne(ra => ra.Report)
            .WithMany(r => r.Assignments)
            .HasForeignKey(ra => ra.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReportAssignment>()
            .HasOne(ra => ra.AssignedToUser)
            .WithMany(u => u.Assignments)
            .HasForeignKey(ra => ra.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure ReportComment entity
        modelBuilder.Entity<ReportComment>()
            .HasKey(rc => rc.Id);
        modelBuilder.Entity<ReportComment>()
            .HasOne(rc => rc.Report)
            .WithMany(r => r.Comments)
            .HasForeignKey(rc => rc.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReportComment>()
            .HasOne(rc => rc.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(rc => rc.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure OrganizationEvent entity
        modelBuilder.Entity<OrganizationEvent>()
            .HasKey(oe => oe.Id);
        modelBuilder.Entity<OrganizationEvent>()
            .HasOne(oe => oe.CreatedByUser)
            .WithMany()
            .HasForeignKey(oe => oe.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure EventAttendee entity
        modelBuilder.Entity<EventAttendee>()
            .HasKey(ea => ea.Id);
        modelBuilder.Entity<EventAttendee>()
            .HasOne(ea => ea.Event)
            .WithMany(oe => oe.Attendees)
            .HasForeignKey(ea => ea.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EventAttendee>()
            .HasOne(ea => ea.User)
            .WithMany(u => u.EventAttendees)
            .HasForeignKey(ea => ea.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Project entity
        modelBuilder.Entity<Project>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<Project>()
            .HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure ProjectOwner entity
        modelBuilder.Entity<ProjectOwner>()
            .HasKey(po => po.Id);
        modelBuilder.Entity<ProjectOwner>()
            .HasOne(po => po.Project)
            .WithMany(p => p.Owners)
            .HasForeignKey(po => po.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectOwner>()
            .HasOne(po => po.User)
            .WithMany()
            .HasForeignKey(po => po.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
