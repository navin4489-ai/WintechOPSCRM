using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ORO_POS.Infrastructure.Data;
namespace ORO_POS.API;

// Migration generation must never connect to or reset a live database.
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite("Data Source=oro_pos_design.db").Options);
}
