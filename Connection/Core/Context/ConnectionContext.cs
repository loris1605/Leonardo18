using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;

namespace Connection.Core.Context;

public class ConnectionContext : BaseContext
{
    public ConnectionContext() { }
    public ConnectionContext(DbContextOptions<ConnectionContext> options) : base(options) { }
    public DbSet<DbSettings> Settings { get; set; } = null!;
}