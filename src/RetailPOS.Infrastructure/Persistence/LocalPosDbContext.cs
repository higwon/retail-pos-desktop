using Microsoft.EntityFrameworkCore;

namespace RetailPOS.Infrastructure.Persistence;

public sealed class LocalPosDbContext(DbContextOptions<LocalPosDbContext> options) : DbContext(options);
