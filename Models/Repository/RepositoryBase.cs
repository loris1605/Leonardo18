using Microsoft.EntityFrameworkCore;
using Models.Interfaces;
using Models.Tables;
using System.Linq.Expressions;

namespace Models.Repository;

/// <summary>
/// Supplies the concurrency values captured when the record was originally read.
/// Required for updates when the entity has configured concurrency tokens.
/// </summary>
public interface IConcurrencyMap
{
    IReadOnlyDictionary<string, object?> OriginalConcurrencyValues { get; }
}

public interface IRepositoryBase<Ttable> where Ttable : class, IStandardTable, new()
{
    Task<int> Add<TMap>(TMap map, CancellationToken ctk = default) where TMap : IMappable<Ttable>;
    Task<bool> Del(IMap map, CancellationToken ctk = default);
    Task<bool> EsisteNome(IMap map, CancellationToken ctk = default);
    Task<bool> EsisteNomeUpd(IMap map, CancellationToken ctk = default);
    Task<List<TResult>> GetAll<TResult>(Expression<Func<Ttable, TResult>> selector,
        Expression<Func<Ttable, bool>>? predicate = null, CancellationToken ct = default,
        Expression<Func<Ttable, object>>? orderBy = null);
    Task<TMap?> GetById<TMap>(int id, Expression<Func<Ttable, TMap>> selector,
        CancellationToken ctk = default) where TMap : class;
    Task<bool> Upd<TMap>(TMap dto, CancellationToken ctk = default)
        where TMap : IMappable<Ttable>, IMap;
}

/// <summary>
/// Performs independent CRUD operations, each with a short-lived context.
/// Database, mapping and cancellation exceptions propagate to the caller.
/// Use ExecuteWriteAsync for related writes; do not call Add/Upd/Del inside its callback.
/// </summary>
public abstract class RepositoryBase<TContext, Ttable> : IRepositoryBase<Ttable>
    where TContext : DbContext, new()
    where Ttable : class, IStandardTable, new()
{
#if DEBUG
    ~RepositoryBase() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    protected readonly Func<TContext> ContextFactory;

    protected RepositoryBase() : this(() => new TContext()) { }

    protected RepositoryBase(Func<TContext> contextFactory)
    {
        ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    protected async Task<TResult> UsingContextAsync<TResult>(Func<TContext, Task<TResult>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        await using var ctx = ContextFactory();
        return await func(ctx).ConfigureAwait(false);
    }

    protected TResult UsingContext<TResult>(Func<TContext, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        using var ctx = ContextFactory();
        return func(ctx);
    }

    /// <summary>
    /// Stages related changes in one context and saves them once, atomically.
    /// The callback must only stage changes, without calling SaveChanges itself.
    /// Return an entity to obtain its generated Id after saving.
    /// </summary>
    protected async Task<TResult> ExecuteWriteAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> action,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        TResult result = await action(ctx, ctk).ConfigureAwait(false);
        await ctx.SaveChangesAsync(ctk).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Coordinates workflows that require several SaveChanges calls.
    /// All work must use the supplied context and a relational database provider.
    /// </summary>
    protected async Task<TResult> UsingTransactionAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> action,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        await using var transaction = await ctx.Database.BeginTransactionAsync(ctk).ConfigureAwait(false);
        TResult result = await action(ctx, ctk).ConfigureAwait(false);
        await ctx.SaveChangesAsync(ctk).ConfigureAwait(false);
        await transaction.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Override for computed names or names obtained through navigation properties.
    /// The returned expression must be translatable to SQL.
    /// Name checks are advisory; uniqueness must be enforced by a database constraint.
    /// </summary>
    protected virtual Expression<Func<Ttable, bool>> CreateNamePredicate(TContext ctx, IMap map)
    {
        if (ctx.Model.FindEntityType(typeof(Ttable))?.FindProperty(nameof(IStandardTable.Nome)) is null)
            throw new NotSupportedException(
                $"{typeof(Ttable).Name}.Nome non è mappato. Ridefinire CreateNamePredicate nel repository specifico.");
        string name = map.Nome;
        return entity => entity.Nome == name;
    }

    public virtual async Task<bool> EsisteNome(IMap map, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        return await ctx.Set<Ttable>().AsNoTracking()
            .AnyAsync(CreateNamePredicate(ctx, map), ctk).ConfigureAwait(false);
    }

    public virtual async Task<bool> EsisteNomeUpd(IMap map, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        return await ctx.Set<Ttable>().AsNoTracking()
            .Where(entity => entity.Id != map.Id)
            .AnyAsync(CreateNamePredicate(ctx, map), ctk).ConfigureAwait(false);
    }

    public virtual async Task<int> Add<TMap>(TMap map, CancellationToken ctk = default)
        where TMap : IMappable<Ttable>
    {
        ArgumentNullException.ThrowIfNull(map);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        Ttable entity = map.ToTable()
            ?? throw new InvalidOperationException("ToTable non può restituire null.");
        ctx.Set<Ttable>().Add(entity);
        await ctx.SaveChangesAsync(ctk).ConfigureAwait(false);
        return entity.Id;
    }

    public virtual async Task<bool> Upd<TMap>(TMap dto, CancellationToken ctk = default)
        where TMap : IMappable<Ttable>, IMap
    {
        ArgumentNullException.ThrowIfNull(dto);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        Ttable? entity = await ctx.Set<Ttable>().FindAsync([dto.Id], ctk).ConfigureAwait(false);
        if (entity is null) return false;

        ApplyOriginalConcurrencyValues(ctx, entity, dto);
        dto.UpdateTable(entity);
        if (entity.Id != dto.Id)
            throw new InvalidOperationException("UpdateTable non può modificare la chiave primaria.");
        await ctx.SaveChangesAsync(ctk).ConfigureAwait(false);
        return true;
    }

    public virtual async Task<bool> Del(IMap map, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        Ttable? entity = await ctx.Set<Ttable>().FindAsync([map.Id], ctk).ConfigureAwait(false);
        if (entity is null) return false;

        ApplyOriginalConcurrencyValues(ctx, entity, map);
        ctx.Set<Ttable>().Remove(entity);
        await ctx.SaveChangesAsync(ctk).ConfigureAwait(false);
        return true;
    }

    private static void ApplyOriginalConcurrencyValues(TContext ctx, Ttable entity, object map)
    {
        var entry = ctx.Entry(entity);
        foreach (var property in entry.Metadata.GetProperties().Where(p => p.IsConcurrencyToken))
        {
            if (map is not IConcurrencyMap concurrencyMap ||
                !concurrencyMap.OriginalConcurrencyValues.TryGetValue(property.Name, out object? originalValue))
                throw new InvalidOperationException(
                    $"Il mapping deve fornire il valore originale del token di concorrenza {property.Name}.");
            entry.Property(property.Name).OriginalValue = originalValue;
        }
    }

    public virtual async Task<TMap?> GetById<TMap>(int id,
        Expression<Func<Ttable, TMap>> selector, CancellationToken ctk = default) where TMap : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        ctk.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        return await ctx.Set<Ttable>().AsNoTracking().Where(entity => entity.Id == id)
            .Select(selector).FirstOrDefaultAsync(ctk).ConfigureAwait(false);
    }

    public virtual async Task<List<TResult>> GetAll<TResult>(
        Expression<Func<Ttable, TResult>> selector,
        Expression<Func<Ttable, bool>>? predicate = null, CancellationToken ct = default,
        Expression<Func<Ttable, object>>? orderBy = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ct.ThrowIfCancellationRequested();
        await using var ctx = ContextFactory();
        IQueryable<Ttable> query = ctx.Set<Ttable>().AsNoTracking();
        if (predicate is not null) query = query.Where(predicate);
        if (orderBy is not null) query = query.OrderBy(orderBy);
        return await query.Select(selector).ToListAsync(ct).ConfigureAwait(false);
    }
}