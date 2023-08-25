using DragonBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Context
{
    public class DragonContext : DbContext
    {
        public DragonContext(DbContextOptions<DragonContext> options) : base(options) { }

        public DbSet<ClanMember> ClanMembers { get; set; }
        public DbSet<Vouch> Vouches { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string solutionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string databasePath = Path.Combine(solutionDirectory, "Dragon.db");

            optionsBuilder.UseSqlite($"Data Source={databasePath}");
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ClanMember>().Property(c => c.Id).ValueGeneratedOnAdd();
            model.Entity<ClanMember>().HasMany(e => e.Vouches).WithOne(e => e.ClanMember).HasForeignKey(e => e.ClanMemberId).HasPrincipalKey(e => e.Id);
        }
    }

    public class DragonContextFactory : IDesignTimeDbContextFactory<DragonContext>
    {
        public DragonContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DragonContext>(); string solutionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string databasePath = Path.Combine(solutionDirectory, "Dragon.db");

            optionsBuilder.UseSqlite($"Data Source={databasePath}");
            //optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DragonContext;Trusted_Connection=True;MultipleActiveResultSets=true",
            //    x => x.MigrationsAssembly("DragonBot"));
            return new DragonContext(optionsBuilder.Options);
        }
    }
}
