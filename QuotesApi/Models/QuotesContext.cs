using Microsoft.EntityFrameworkCore;
using System;

namespace QuotesApi.Models
{
    public class QuotesContext : DbContext
    {
        private readonly string connectionString =
            System.Environment.GetEnvironmentVariable($"mysqlConnectionString");

        public DbSet<Quote> Quotes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

    }
}
