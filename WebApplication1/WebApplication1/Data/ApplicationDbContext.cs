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
    public DbSet<ProjectGroupAssignment> ProjectGroupAssignments { get; set; }

    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ProjectApprovalRequest> ProjectApprovalRequests { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatRoom> ChatRooms { get; set; }
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; }
    public DbSet<ChatRoomMessage> ChatRoomMessages { get; set; }
    public DbSet<ProjectProgressLog> ProjectProgressLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<ProjectGroup>().HasKey(g => g.Id);

        modelBuilder.Entity<Project>().HasKey(p => p.Id);
        modelBuilder.Entity<Project>().HasOne(p => p.CreatedByUser).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectGroupAssignment>().HasKey(pga => pga.Id);
        modelBuilder.Entity<ProjectGroupAssignment>().HasOne(pga => pga.Project).WithMany(p => p.Groups).HasForeignKey(pga => pga.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectGroupAssignment>().HasOne(pga => pga.Group).WithMany(g => g.ProjectAssignments).HasForeignKey(pga => pga.GroupId).OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<ChatMessage>().HasKey(c => c.Id);
        modelBuilder.Entity<ChatMessage>().HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatRoom>().HasKey(r => r.Id);
        modelBuilder.Entity<ChatRoom>().HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatRoomMember>().HasKey(m => m.Id);
        modelBuilder.Entity<ChatRoomMember>().HasIndex(m => new { m.RoomId, m.UserId }).IsUnique();
        modelBuilder.Entity<ChatRoomMember>().HasOne(m => m.Room).WithMany(r => r.Members).HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChatRoomMember>().HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatRoomMessage>().HasKey(m => m.Id);
        modelBuilder.Entity<ChatRoomMessage>().HasOne(m => m.Room).WithMany(r => r.Messages).HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChatRoomMessage>().HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectProgressLog>().HasKey(pl => pl.Id);
        modelBuilder.Entity<ProjectProgressLog>().HasOne(pl => pl.Project).WithMany().HasForeignKey(pl => pl.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectProgressLog>().HasOne(pl => pl.User).WithMany().HasForeignKey(pl => pl.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
