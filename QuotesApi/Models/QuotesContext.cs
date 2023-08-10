using Microsoft.EntityFrameworkCore;
using System;

namespace QuotesApi.Models
{
    public class QuotesContext : DbContext
    {
        public DbSet<Quote> Quote { get; set; }

        public string DbPath { get; }

        public QuotesContext()
        {
            var path = Environment.CurrentDirectory;
            DbPath = System.IO.Path.Join(path, "\\quotes.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

    }
}
