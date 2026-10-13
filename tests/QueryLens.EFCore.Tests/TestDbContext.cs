using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QueryLens.EFCore.Tests
{
    public class TestCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<TestOrder> Orders { get; set; }
    }

    public class TestOrder
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal Total { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<TestCustomer> Customers { get; set; }
        public DbSet<TestOrder> Orders { get; set; }
    }
}
