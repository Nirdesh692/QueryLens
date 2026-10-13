using System.Collections.Generic;
using System.Data.Entity;

namespace NetFrameworkEf6Sample
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal Total { get; set; }
        public virtual Customer Customer { get; set; }
    }

    public class SampleContext : DbContext
    {
        public SampleContext() : base("name=SampleContext")
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
    }
}
