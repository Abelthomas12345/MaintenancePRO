using MaintenancePRO.Api.Models; using Microsoft.AspNetCore.Identity.EntityFrameworkCore; using Microsoft.EntityFrameworkCore;
namespace MaintenancePRO.Api.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options):IdentityDbContext<ApplicationUser>(options) {
 public DbSet<Equipment> Equipments=>Set<Equipment>(); public DbSet<Breakdown> Breakdowns=>Set<Breakdown>();
 public DbSet<Diagnosis> Diagnoses=>Set<Diagnosis>(); public DbSet<Intervention> Interventions=>Set<Intervention>();
 public DbSet<PhotoRecord> Photos=>Set<PhotoRecord>(); public DbSet<AuditEvent> AuditEvents=>Set<AuditEvent>();
 protected override void OnModelCreating(ModelBuilder b){base.OnModelCreating(b);
 b.Entity<Equipment>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Breakdown>().HasIndex(x=>new{x.Status,x.ReportedUtc});
 b.Entity<Intervention>().HasIndex(x=>new{x.Status,x.CreatedUtc}); b.Entity<AuditEvent>().HasIndex(x=>x.CreatedUtc);
 b.Entity<Breakdown>().HasOne(x=>x.Equipment).WithMany().HasForeignKey(x=>x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Diagnosis>().HasOne(x=>x.Breakdown).WithMany(x=>x.Diagnoses).HasForeignKey(x=>x.BreakdownId).OnDelete(DeleteBehavior.Cascade);
 b.Entity<Intervention>().HasOne(x=>x.Breakdown).WithMany(x=>x.Interventions).HasForeignKey(x=>x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
 b.Entity<PhotoRecord>().HasOne(x=>x.Breakdown).WithMany(x=>x.Photos).HasForeignKey(x=>x.BreakdownId).OnDelete(DeleteBehavior.Cascade);
 b.Entity<PhotoRecord>().HasOne(x=>x.Intervention).WithMany().HasForeignKey(x=>x.InterventionId).OnDelete(DeleteBehavior.Cascade);
 b.Entity<Intervention>().Property(x=>x.Cost).HasPrecision(12,2);
 }}
