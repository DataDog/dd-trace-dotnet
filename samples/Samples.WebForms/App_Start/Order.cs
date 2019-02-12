namespace Samples.WebForms
{
    public class Order
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Status { get; set; }
    }

    public class OrderItem
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public double UnitCost { get; set; }
    }
}
