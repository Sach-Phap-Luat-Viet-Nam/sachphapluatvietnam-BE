using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Infrastructure.Persistence;

/// <summary>
/// Schema <c>ordering</c>. <c>user_id</c>, <c>sku_id</c> tham chiếu module khác nhưng không đặt khóa ngoại chéo schema;
/// đơn đã lưu snapshot nên không phụ thuộc dữ liệu Catalog về sau.
/// </summary>
internal sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public const string Schema = "ordering";

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderCodeCounter> OrderCodeCounters => Set<OrderCodeCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders", table => table.HasCheckConstraint(
                "ck_orders_amounts", "subtotal >= 0 AND discount >= 0 AND shipping_fee >= 0 AND total >= 0"));
            order.Property(o => o.OrderCode).HasMaxLength(20);
            order.HasIndex(o => o.OrderCode).IsUnique();
            order.Property(o => o.CustomerName).HasMaxLength(255);
            order.Property(o => o.Phone).HasMaxLength(20);
            order.Property(o => o.Email).HasMaxLength(256);
            order.Property(o => o.Note).HasMaxLength(1000);
            order.Property(o => o.IdempotencyKey).HasMaxLength(64);
            order.HasIndex(o => o.IdempotencyKey).IsUnique();
            order.Property(o => o.Subtotal).HasPrecision(12, 0);
            order.Property(o => o.Discount).HasPrecision(12, 0);
            order.Property(o => o.ShippingFee).HasPrecision(12, 0);
            order.Property(o => o.Total).HasPrecision(12, 0);
            order.HasIndex(o => new { o.UserId, o.CreatedAt });
            order.HasIndex(o => new { o.Status, o.CreatedAt });
            order.HasIndex(o => o.PaymentExpiresAt).HasFilter("payment_expires_at IS NOT NULL");

            order.OwnsOne(o => o.ShippingAddress, address => address.ToJson("shipping_address"));
            order.OwnsOne(o => o.InvoiceInfo, invoice => invoice.ToJson("invoice_info"));

            order.HasMany(o => o.Items).WithOne().HasForeignKey(item => item.OrderId);
            order.HasMany(o => o.StatusLogs).WithOne().HasForeignKey(log => log.OrderId);
        });

        modelBuilder.Entity<OrderItem>(item =>
        {
            item.ToTable("order_items", table => table.HasCheckConstraint("ck_order_items_quantity", "quantity > 0"));
            item.Property(i => i.SkuCode).HasMaxLength(50);
            item.Property(i => i.BookTitle).HasMaxLength(255);
            item.Property(i => i.EditionName).HasMaxLength(100);
            item.Property(i => i.Isbn).HasMaxLength(13).IsFixedLength();
            item.Property(i => i.CoverType).HasMaxLength(30);
            item.Property(i => i.UnitPrice).HasPrecision(12, 0);
            item.Property(i => i.LineTotal).HasPrecision(12, 0);
            item.Property(i => i.CostPrice).HasPrecision(12, 0);
        });

        modelBuilder.Entity<OrderStatusLog>(log =>
        {
            log.ToTable("order_status_logs");
            log.Property(l => l.Note).HasMaxLength(1000);
        });

        modelBuilder.Entity<OrderCodeCounter>(counter =>
        {
            counter.ToTable("order_code_counters");
            counter.HasKey(c => c.Day);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MapEnum<OrderStatus>(configurationBuilder);
        MapEnum<PaymentMethod>(configurationBuilder);
        MapEnum<PaymentStatus>(configurationBuilder);
        MapEnum<OrderType>(configurationBuilder);
    }

    private static void MapEnum<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum =>
        configurationBuilder.Properties<TEnum>()
            .HaveConversion<SnakeCaseEnumConverter<TEnum>>()
            .HaveMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);
}
