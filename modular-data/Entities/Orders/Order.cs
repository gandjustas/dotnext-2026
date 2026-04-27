namespace Orders;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public decimal Amount { get; set; }
}