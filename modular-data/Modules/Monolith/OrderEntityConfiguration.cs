using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Orders;
using Customers;

class OrderEntityConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.HasOne<Customer>()
              .WithMany()
              .HasForeignKey(e => e.CustomerId);
    }
}
