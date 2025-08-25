using Common.Entities;

namespace Catalog.API.Domain.Entities
{
    public enum ProductStatus
    {
        Active,
        Inactive,
        OutOfStock
    }

    public class Product : Entity
    {
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
        public string SKU { get; private set; } = string.Empty;
        public int CategoryId { get; private set; }
        public ProductStatus Status { get; private set; }
        public string ImageUrl { get; private set; } = string.Empty;
        public int StockQuantity { get; private set; }

        // Navigation properties
        public Category Category { get; private set; } = null!;

        private Product() { } // EF Core

        public Product(string name, string description, decimal price, string sku, int categoryId, string imageUrl, int stockQuantity)
        {
            Name = name;
            Description = description;
            Price = price;
            SKU = sku;
            CategoryId = categoryId;
            ImageUrl = imageUrl;
            StockQuantity = stockQuantity;
            Status = ProductStatus.Active;

            AddDomainEvent(new ProductCreatedEvent(Id, Name));
        }

        public void UpdatePrice(decimal newPrice)
        {
            if (newPrice <= 0) 
                throw new ArgumentException("Price must be positive");

            var oldPrice = Price;
            Price = newPrice;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ProductPriceUpdatedEvent(Id, oldPrice, newPrice));
        }

        public void UpdateStock(int newQuantity)
        {
            if (newQuantity < 0) 
                throw new ArgumentException("Stock quantity cannot be negative");

            StockQuantity = newQuantity;
            Status = newQuantity == 0 ? ProductStatus.OutOfStock : ProductStatus.Active;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ProductStockUpdatedEvent(Id, newQuantity));
        }
    }

    public class Category : Entity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        
        // Navigation properties
        public Category? Parent { get; set; }
        public ICollection<Category> Children { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    // Domain Events
    public record ProductCreatedEvent(int ProductId, string ProductName) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ProductPriceUpdatedEvent(int ProductId, decimal OldPrice, decimal NewPrice) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ProductStockUpdatedEvent(int ProductId, int NewQuantity) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}