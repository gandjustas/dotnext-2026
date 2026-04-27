namespace Customers;

using System.ComponentModel.DataAnnotations;

public class Customer
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(50)]
    [EmailAddress]
    public string Email { get; set; } = null!;
}