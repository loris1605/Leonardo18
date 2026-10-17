using Microsoft.EntityFrameworkCore;
using Models.Context;

namespace Models.Repository
{
    public static class DatabaseHelper
    {
        public static async Task<bool> HasPendingMigrationsAsync(CancellationToken ct = default)
        {
            // Cost: una query su __EFMigrationsHistory + confronto in memoria
            using AppDbContext ctx = new();
            var pending = await ctx.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false);
            return pending.Any();
        }

        public static async Task ApplyMigrationsIfNeededAsync(CancellationToken ct = default)
        {
            using AppDbContext ctx = new();
            await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
           
        }
    }
}
