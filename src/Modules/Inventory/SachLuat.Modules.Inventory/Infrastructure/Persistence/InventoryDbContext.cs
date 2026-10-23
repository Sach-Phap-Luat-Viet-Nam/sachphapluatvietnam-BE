using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Schema <c>inventory</c>. <c>sku_id</c> tham chiếu <c>catalog.skus</c> nhưng không đặt khóa ngoại chéo schema
/// để giữ ranh giới module; SKU được kiểm tra qua <c>ICatalogSkuReader</c>.
/// </summary>
internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<PrintBatch> PrintBatches => Set<PrintBatch>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<InventoryTransaction> Transactions => Set<InventoryTransaction>();

    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StockItem>(stock =>
        {
            stock.ToTable("stock_items", table =>
            {
                table.HasCheckConstraint("ck_stock_items_available", "available >= 0");
                table.HasCheckConstraint("ck_stock_items_reserved", "reserved >= 0");
            });
            stock.HasKey(item => item.SkuId);
            stock.Property(item => item.SkuId).ValueGeneratedNever();
        });

        modelBuilder.Entity<PrintBatch>(batch =>
        {
            batch.ToTable("print_batches", table =>
            {
                table.HasCheckConstraint("ck_print_batches_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_print_batches_remaining", "remaining >= 0 AND remaining <= quantity");
            });
            batch.Property(b => b.BatchCode).HasMaxLength(50);
            batch.Property(b => b.PrinterName).HasMaxLength(255);
            batch.Property(b => b.UnitCost).HasPrecision(12, 0);
            batch.HasIndex(b => new { b.SkuId, b.BatchCode }).IsUnique();
            // FIFO: lấy lô cũ nhất còn hàng.
            batch.HasIndex(b => new { b.SkuId, b.ReceivedAt, b.Id });
        });

        modelBuilder.Entity<GoodsReceipt>(receipt =>
        {
            receipt.ToTable("goods_receipts");
            receipt.Property(r => r.Note).HasMaxLength(1000);
            receipt.Property(r => r.RejectReason).HasMaxLength(1000);
            receipt.HasIndex(r => r.Status);
            receipt.HasMany(r => r.Lines).WithOne().HasForeignKey(line => line.GoodsReceiptId);
        });

        modelBuilder.Entity<GoodsReceiptLine>(line =>
        {
            line.ToTable("goods_receipt_lines", table =>
            {
                table.HasCheckConstraint("ck_goods_receipt_lines_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_goods_receipt_lines_unit_cost", "unit_cost >= 0");
            });
            line.Property(l => l.BatchCode).HasMaxLength(50);
            line.Property(l => l.PrinterName).HasMaxLength(255);
            line.Property(l => l.UnitCost).HasPrecision(12, 0);
        });

        modelBuilder.Entity<InventoryTransaction>(transaction =>
        {
            transaction.ToTable("inventory_transactions");
            transaction.Property(t => t.Reference).HasMaxLength(50);
            transaction.Property(t => t.Note).HasMaxLength(1000);
            transaction.HasIndex(t => new { t.SkuId, t.CreatedAt });
            transaction.HasIndex(t => t.Reference);
            transaction.HasOne<PrintBatch>().WithMany().HasForeignKey(t => t.PrintBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReservation>(reservation =>
        {
            reservation.ToTable("stock_reservations", table =>
                table.HasCheckConstraint("ck_stock_reservations_quantity", "quantity > 0"));
            reservation.Property(r => r.Reference).HasMaxLength(50);
            reservation.HasIndex(r => new { r.Reference, r.SkuId }).IsUnique();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MapEnum<ReceiptStatus>(configurationBuilder);
        MapEnum<TransactionType>(configurationBuilder);
        MapEnum<ReservationStatus>(configurationBuilder);
    }

    private static void MapEnum<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum =>
        configurationBuilder.Properties<TEnum>()
            .HaveConversion<SnakeCaseEnumConverter<TEnum>>()
            .HaveMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);
}
