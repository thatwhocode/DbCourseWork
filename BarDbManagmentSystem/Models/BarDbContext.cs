using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BarDbManagmentSystem.Models;

public partial class BarDbContext : DbContext
{
    public BarDbContext()
    {
    }

    public BarDbContext(DbContextOptions<BarDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BarSystemLog> BarSystemLogs { get; set; }

    public virtual DbSet<BarTable> BarTables { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Hall> Halls { get; set; }

    public virtual DbSet<Ingredient> Ingredients { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Recipe> Recipes { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<Staff> Staff { get; set; }

    public virtual DbSet<StaffLanguage> StaffLanguages { get; set; }

    public virtual DbSet<StaffSpeciality> StaffSpecialities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=tcp:127.0.0.1,1433;Initial Catalog=BarDB;User Id=sa;Password=Super@Bar#Server2026;Encrypt=False;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Ukrainian_CI_AS");

        modelBuilder.Entity<BarSystemLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__BarSyste__2D21E3B6F9A47297");

            entity.Property(e => e.LogId).HasColumnName("Log_id");
            entity.Property(e => e.ActionDescription)
                .IsUnicode(false)
                .HasColumnName("Action_description");
            entity.Property(e => e.AppUser)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("App_user");
            entity.Property(e => e.LogDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Log_date");
        });

        modelBuilder.Entity<BarTable>(entity =>
        {
            entity.HasKey(e => e.TableId).HasName("PK__BarTable__B5731FEEC0CA0995");

            entity.HasIndex(e => e.TableId, "UX_BarTables_TableNumber").IsUnique();

            entity.Property(e => e.TableId).HasColumnName("Table_id");
            entity.Property(e => e.HallId).HasColumnName("Hall_id");
            entity.Property(e => e.Name).HasMaxLength(50);

            entity.HasOne(d => d.Hall).WithMany(p => p.BarTables)
                .HasForeignKey(d => d.HallId)
                .HasConstraintName("Fk_BarTables_Hall_id");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__6DB28136A115C1C5");

            entity.ToTable("Category");

            entity.Property(e => e.CategoryId).HasColumnName("Category_id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.HasKey(e => e.HallId).HasName("PK__Halls__9235201E08A7B3A9");

            entity.Property(e => e.HallId).HasColumnName("Hall_id");
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.IngredientId).HasName("PK__Ingredie__C90398E34961DACF");

            entity.Property(e => e.IngredientId).HasColumnName("Ingredient_id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Unit)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__MenuItem__3FB403AC895C4F66");

            entity.HasIndex(e => e.Name, "IX_Menu_Index_Name");

            entity.Property(e => e.ItemId).HasColumnName("Item_id");
            entity.Property(e => e.CategoryId).HasColumnName("Category_id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Category).WithMany(p => p.MenuItems)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("Fk_MenuIems_Category_id");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__F1FF845366F037AD");

            entity.ToTable(tb => tb.HasTrigger("trg_LogOrderChanges"));

            entity.HasIndex(e => e.OrderDate, "IX_Orders_ActiveOrders").HasFilter("([Is_completed]=(0))");

            entity.HasIndex(e => new { e.IsCompleted, e.OrderDate }, "IX_Orders_CoveringIndex");

            entity.HasIndex(e => new { e.IsCompleted, e.OrderId }, "Ix_TotalSales_Perfomance");

            entity.Property(e => e.OrderId).HasColumnName("Order_id");
            entity.Property(e => e.IsCompleted).HasColumnName("Is_completed");
            entity.Property(e => e.OrderDate).HasDefaultValueSql("GETDATE()")
                .HasColumnName("Order_date");
            entity.Property(e => e.StaffId).HasColumnName("Staff_id");
            entity.Property(e => e.TableNumber).HasColumnName("Table_number");

            entity.HasOne(d => d.Staff).WithMany(p => p.Orders)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("Fk_Orders_Staff_id");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ItemId }).HasName("PK__OrderDet__2204C469DA299E73");

            entity.ToTable(tb => tb.HasTrigger("trg_CheckStockOnInsert"));

            entity.HasIndex(e => e.OrderId, "IX_OrderDetails_OrderId_Include");

            entity.Property(e => e.OrderId).HasColumnName("Order_id");
            entity.Property(e => e.ItemId).HasColumnName("Item_id");
            entity.Property(e => e.SalePrice)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("Sale_price");

            entity.HasOne(d => d.Item).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Fk_OrderDetails_Items");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Fk_OrderDetails_Orders");
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.RecipeId).HasName("PK__Recipes__0958CAF10EBF0B07");

            entity.Property(e => e.RecipeId).HasColumnName("Recipe_id");
            entity.Property(e => e.IngredientId).HasColumnName("Ingredient_id");
            entity.Property(e => e.ItemId).HasColumnName("Item_id");
            entity.Property(e => e.Quantity).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.Recipes)
                .HasForeignKey(d => d.IngredientId)
                .HasConstraintName("Fk_Recipes_Ingredient_id");

            entity.HasOne(d => d.Item).WithMany(p => p.Recipes)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("Fk_Recipes_MenuItem");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("PK__Shifts__527BDABFAC932F35");

            entity.Property(e => e.ShiftId).HasColumnName("Shift_id");
            entity.Property(e => e.ShiftType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Shift_type");
            entity.Property(e => e.StaffId).HasColumnName("Staff_id");
            entity.Property(e => e.WorkDate)
                .HasColumnType("datetime")
                .HasColumnName("Work_date");

            entity.HasOne(d => d.Staff).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Fk_Shifts_Staff_id");

            entity.HasMany(d => d.Halls).WithMany(p => p.Shifts)
                .UsingEntity<Dictionary<string, object>>(
                    "ShiftHall",
                    r => r.HasOne<Hall>().WithMany()
                        .HasForeignKey("HallId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Fk_ShiftHalls_Hall_id"),
                    l => l.HasOne<Shift>().WithMany()
                        .HasForeignKey("ShiftId")
                        .HasConstraintName("Fk_ShiftHalls_Shift_id"),
                    j =>
                    {
                        j.HasKey("ShiftId", "HallId").HasName("PK__ShiftHal__AB5888BEC9CA32F2");
                        j.ToTable("ShiftHalls");
                        j.IndexerProperty<int>("ShiftId").HasColumnName("Shift_id");
                        j.IndexerProperty<int>("HallId").HasColumnName("Hall_id");
                    });
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.StaffId).HasName("PK__Staff__32D2E85B41B572F8");

            entity.Property(e => e.StaffId).HasColumnName("Staff_id");
            entity.Property(e => e.FullName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Full_name");
            entity.Property(e => e.Position)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasMany(d => d.Halls).WithMany(p => p.Staff)
                .UsingEntity<Dictionary<string, object>>(
                    "StaffHall",
                    r => r.HasOne<Hall>().WithMany()
                        .HasForeignKey("HallId")
                        .HasConstraintName("Fk_Staff_Halls_Hall_id"),
                    l => l.HasOne<Staff>().WithMany()
                        .HasForeignKey("StaffId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Fk_StaffHalls_Staff_id"),
                    j =>
                    {
                        j.HasKey("StaffId", "HallId").HasName("PK__StaffHal__CBF1BA5A0A5F74F2");
                        j.ToTable("StaffHalls");
                        j.IndexerProperty<int>("StaffId").HasColumnName("Staff_id");
                        j.IndexerProperty<int>("HallId").HasColumnName("Hall_id");
                    });

            entity.HasMany(d => d.ShiftsNavigation).WithMany(p => p.StaffNavigation)
                .UsingEntity<Dictionary<string, object>>(
                    "StaffShift",
                    r => r.HasOne<Shift>().WithMany()
                        .HasForeignKey("ShiftId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Fk_StaffShifts_Shift_id"),
                    l => l.HasOne<Staff>().WithMany()
                        .HasForeignKey("StaffId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Fk_StaffShifts_Staff_id"),
                    j =>
                    {
                        j.HasKey("StaffId", "ShiftId").HasName("PK__StaffShi__D7F555F02C8D4558");
                        j.ToTable("StaffShifts");
                        j.IndexerProperty<int>("StaffId").HasColumnName("Staff_id");
                        j.IndexerProperty<int>("ShiftId").HasColumnName("Shift_id");
                    });
        });

        modelBuilder.Entity<StaffLanguage>(entity =>
        {
            entity.HasKey(e => new { e.StaffId, e.Languages }).HasName("PK__StaffLan__4B6C138E8EBDA272");

            entity.Property(e => e.StaffId).HasColumnName("Staff_id");
            entity.Property(e => e.Languages).HasMaxLength(50);

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffLanguages)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("Fk_Staff_Languages_Staff_id");
        });

        modelBuilder.Entity<StaffSpeciality>(entity =>
        {
            entity.HasKey(e => new { e.StaffId, e.Specialization }).HasName("PK__StaffSpe__F7CAE07D70D0263C");

            entity.Property(e => e.StaffId).HasColumnName("Staff_id");
            entity.Property(e => e.Specialization).HasMaxLength(50);

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffSpecialities)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("Fk_StaffSpecialities_Staff_id");
        });
        modelBuilder.HasSequence("Seq_MenuId").StartsAt(1000L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
