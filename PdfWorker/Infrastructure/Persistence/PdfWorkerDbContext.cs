using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class PdfWorkerDbContext : DbContext
{
    public PdfWorkerDbContext(DbContextOptions<PdfWorkerDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
}
