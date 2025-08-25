using Common.Entities;

namespace Basket.API.Models
{
    public class ShoppingCart : Entity
    {
        public int UserId { get; set; }
        public List<CartItem> Items { get; set; } = new();

        public decimal TotalAmount => Items.Sum(item => item.TotalPrice);
        public int TotalItems => Items.Sum(item => item.Quantity);

        public void AddItem(int productId, string productName, decimal price, int quantity, string imageUrl)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == productId);
            
            if (existingItem != null)
            {
                existingItem.UpdateQuantity(existingItem.Quantity + quantity);
            }
            else
            {
                Items.Add(new CartItem
                {
                    ProductId = productId,
                    ProductName = productName,
                    Price = price,
                    Quantity = quantity,
                    ImageUrl = imageUrl
                });
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateItemQuantity(int productId, int newQuantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (newQuantity <= 0)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.UpdateQuantity(newQuantity);
                }
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void RemoveItem(int productId)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                Items.Remove(item);
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void ClearCart()
        {
            Items.Clear();
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public decimal TotalPrice => Price * Quantity;

        public void UpdateQuantity(int newQuantity)
        {
            if (newQuantity <= 0) throw new ArgumentException("Quantity must be positive");
            Quantity = newQuantity;
        }
    }
}