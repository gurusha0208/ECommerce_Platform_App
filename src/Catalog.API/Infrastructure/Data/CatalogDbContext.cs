using Catalog.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Infrastructure.Data
{
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Description).HasMaxLength(1000);
                entity.Property(c => c.ImageUrl).HasMaxLength(500);
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                entity.Property(p => p.SKU).IsRequired().HasMaxLength(50);
                entity.HasIndex(p => p.SKU).IsUnique();
                
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Ignore(p => p.DomainEvents);
            });

            // Category configuration
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(500);
                entity.Property(c => c.ImageUrl).HasMaxLength(500);
                entity.Property(c => c.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(c => c.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(c => c.Parent)
                    .WithMany(c => c.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>().HasData(
                new { Id = 1, Name = "Electronics", Description = "Electronic devices and gadgets" , ImageUrl = "/electronics.jpg"},
                new { Id = 2, Name = "Clothing", Description = "Fashion and apparel" ,ImageUrl = "/clothing.jpg"},
                new { Id = 3, Name = "Books", Description = "Books and literature" ,ImageUrl = "/books.png" },
                new { Id = 4, Name = "Smartphones", Description = "Mobile phones", ParentId = 1 ,ImageUrl = "/smartphones.jpeg"},
                new { Id = 5, Name = "Laptops", Description = "Portable computers", ParentId = 1 ,ImageUrl = "/laptops.jpeg"}
            );
        }
    }
}