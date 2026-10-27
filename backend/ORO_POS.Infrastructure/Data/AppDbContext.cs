using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.Entities;
using System.Linq.Expressions;

namespace ORO_POS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<ModifierOption> ModifierOptions => Set<ModifierOption>();
    public DbSet<ProductModifierGroup> ProductModifierGroups => Set<ProductModifierGroup>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<KitchenTicket> KitchenTickets => Set<KitchenTicket>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<InventoryLedger> InventoryLedgers => Set<InventoryLedger>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete global filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, "IsDeleted");
                var notProperty = Expression.Not(property);
                var lambda = Expression.Lambda(notProperty, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(u => u.Outlet).WithMany(o => o.Users).HasForeignKey(u => u.OutletId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.SKU);
            e.HasIndex(p => p.Barcode);
            e.HasOne(p => p.Outlet).WithMany(o => o.Products).HasForeignKey(p => p.OutletId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CreatedAt);
            e.HasOne(o => o.Outlet).WithMany(o => o.Orders).HasForeignKey(o => o.OutletId);
            e.HasOne(o => o.Table).WithMany(t => t.Orders).HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(o => o.User).WithMany(u => u.Orders).HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.Shift).WithMany().HasForeignKey(o => o.ShiftId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasIndex(e => e.OrderId);
            e.HasIndex(e => e.ProductId);
            e.HasOne(e => e.Order).WithMany(o => o.OrderItems).HasForeignKey(e => e.OrderId);
            e.HasOne(e => e.Product).WithMany(p => p.OrderItems).HasForeignKey(e => e.ProductId);
            e.Property(e => e.LineTotal).HasPrecision(18, 2);
            e.Property(e => e.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(e => e.Subtotal).HasPrecision(18, 2);
            e.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            e.Property(e => e.TaxAmount).HasPrecision(18, 2);
            e.Property(e => e.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasIndex(e => e.OrderId);
            e.HasIndex(e => e.Status);
            e.HasOne(p => p.Order).WithMany(o => o.Payments).HasForeignKey(p => p.OrderId);
            e.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.HasIndex(e => e.Status);
            e.HasOne(s => s.User).WithMany(u => u.Shifts).HasForeignKey(s => s.UserId);
            e.HasOne(s => s.Outlet).WithMany(o => o.Shifts).HasForeignKey(s => s.OutletId);
            e.Property(e => e.OpeningFloat).HasPrecision(18, 2);
            e.Property(e => e.PaidIn).HasPrecision(18, 2);
            e.Property(e => e.PaidOut).HasPrecision(18, 2);
            e.Property(e => e.CountedCash).HasPrecision(18, 2);
            e.Property(e => e.Variance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<InventoryLedger>(e =>
        {
            e.HasIndex(e => e.ProductId);
            e.HasIndex(e => e.MovementType);
            e.HasIndex(e => e.CreatedAt);
            e.HasOne(i => i.Product).WithMany(p => p.InventoryLedgers).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.Actor).WithMany(u => u.InventoryMovements).HasForeignKey(i => i.ActorId).OnDelete(DeleteBehavior.SetNull);
            e.Property(e => e.Quantity).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Recipe>(e =>
        {
            e.HasIndex(e => e.ProductId);
            e.HasIndex(e => e.IngredientId);
            e.HasOne(r => r.Product).WithMany(p => p.Recipes).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(r => r.Ingredient).WithMany().HasForeignKey(r => r.IngredientId).OnDelete(DeleteBehavior.NoAction);
            e.Property(e => e.Quantity).HasPrecision(18, 4);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(e => e.Entity);
            e.HasIndex(e => e.CreatedAt);
            e.HasOne(a => a.Actor).WithMany(u => u.AuditLogs).HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Table>(e =>
        {
            e.HasIndex(e => e.OutletId);
            e.HasIndex(e => e.Status);
            e.HasOne(t => t.Outlet).WithMany(o => o.Tables).HasForeignKey(t => t.OutletId);
            e.HasOne(t => t.Zone).WithMany(z => z.Tables).HasForeignKey(t => t.ZoneId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Zone>(e =>
        {
            e.HasIndex(e => e.OutletId);
            e.HasOne(z => z.Outlet).WithMany(o => o.Zones).HasForeignKey(z => z.OutletId);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(e => e.OutletId);
            e.HasOne(c => c.Outlet).WithMany(o => o.Categories).HasForeignKey(c => c.OutletId);
        });

        modelBuilder.Entity<ModifierGroup>(e =>
        {
            e.HasIndex(e => e.OutletId);
            e.HasOne(mg => mg.Outlet).WithMany().HasForeignKey(mg => mg.OutletId);
        });

        modelBuilder.Entity<ModifierOption>(e =>
        {
            e.HasIndex(e => e.ModifierGroupId);
            e.HasOne(mo => mo.ModifierGroup).WithMany(mg => mg.Options).HasForeignKey(mo => mo.ModifierGroupId);
        });

        modelBuilder.Entity<ProductModifierGroup>(e =>
        {
            e.HasKey(pmg => new { pmg.ProductId, pmg.ModifierGroupId });
            e.HasOne(pmg => pmg.Product).WithMany(p => p.ModifierGroups).HasForeignKey(pmg => pmg.ProductId);
            e.HasOne(pmg => pmg.ModifierGroup).WithMany(mg => mg.ProductModifierGroups).HasForeignKey(pmg => pmg.ModifierGroupId);
        });

        modelBuilder.Entity<KitchenTicket>(e =>
        {
            e.HasIndex(e => e.OrderId);
            e.HasIndex(e => e.Status);
            e.HasOne(kt => kt.Order).WithMany(o => o.KitchenTickets).HasForeignKey(kt => kt.OrderId);
        });

        modelBuilder.Entity<DeviceConfig>(e =>
        {
            e.HasIndex(e => e.DeviceName).IsUnique();
            e.HasOne(d => d.Outlet).WithMany().HasForeignKey(d => d.OutletId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
