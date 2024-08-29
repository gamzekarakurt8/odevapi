using Microsoft.EntityFrameworkCore;
using odevapi; // Model sınıflarınızın bulunduğu namespace

namespace odevapi
{
        public class ApplicationDbContext : DbContext
        {
            public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
                : base(options)
            {
            }

            public DbSet<MyDataModel> Tablo { get; set; } // Model sınıfınızın adı, tablo adı ile eşleşir.

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                // Tablo adını belirtin
                modelBuilder.Entity<MyDataModel>().ToTable("tablo"); 
            }
        }
    }
