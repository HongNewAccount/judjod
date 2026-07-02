using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ProjectGroup> ProjectGroups { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectOwner> ProjectOwners { get; set; }

    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ProjectApprovalRequest> ProjectApprovalRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<ProjectGroup>().HasKey(g => g.Id);

        modelBuilder.Entity<Project>().HasKey(p => p.Id);
        modelBuilder.Entity<Project>().HasOne(p => p.Group).WithMany(g => g.Projects).HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Project>().HasOne(p => p.CreatedByUser).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectOwner>().HasKey(po => po.Id);
        modelBuilder.Entity<ProjectOwner>().HasOne(po => po.Project).WithMany(p => p.Owners).HasForeignKey(po => po.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectOwner>().HasOne(po => po.User).WithMany().HasForeignKey(po => po.UserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLog>().HasKey(al => al.Id);
        modelBuilder.Entity<ActivityLog>().HasOne(al => al.Project).WithMany(p => p.ActivityLogs).HasForeignKey(al => al.ProjectId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ActivityLog>().HasOne(al => al.User).WithMany().HasForeignKey(al => al.UserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectApprovalRequest>().HasKey(par => par.Id);
        modelBuilder.Entity<ProjectApprovalRequest>().HasOne(par => par.Project).WithMany().HasForeignKey(par => par.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectApprovalRequest>().HasOne(par => par.RequestedByUser).WithMany().HasForeignKey(par => par.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProjectApprovalRequest>().HasOne(par => par.ApprovedByUser).WithMany().HasForeignKey(par => par.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
