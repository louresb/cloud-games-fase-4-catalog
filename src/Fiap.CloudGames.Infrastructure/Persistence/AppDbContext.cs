using Fiap.CloudGames.Domain.Games.Entities;
using Fiap.CloudGames.Domain.Orders.Entities;
using Fiap.CloudGames.Domain.Promotions.Entities;
using Fiap.CloudGames.Domain.UserGamesLibrary.Entities;
using Fiap.CloudGames.Domain.Carts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Fiap.CloudGames.Infrastructure.Persistence;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // getter-only DbSets: safer (no reassignment) and avoids nullability warnings
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<UserGameLibrary> UserGameLibrary => Set<UserGameLibrary>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
