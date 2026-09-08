namespace DevelKit.MessageTemplates;

/// <summary>Загружает определения, включая неактивные: их вхождения удаляются при раскрытии.</summary>
public interface ITemplateAliasRepository
{
    Task<IReadOnlyList<TemplateAlias>> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>Общий снимок каталога с TTL, последовательным обновлением и явным сбросом.</summary>
/// <remarks>Переиспользуйте экземпляр. Делегат clock позволяет проверять истечение TTL без ожидания.</remarks>
public sealed class CachedTemplateAliases(
    ITemplateAliasRepository repository, TimeSpan? lifetime = null, Func<DateTimeOffset>? clock = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan ttl = lifetime ?? TimeSpan.FromMinutes(30);
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private IReadOnlyList<TemplateAlias>? snapshot;
    private DateTimeOffset expires;
    private long generation;
    private long loadedGeneration = -1;

    /// <summary>Помечает снимок устаревшим. Следующий вызов раскрытия перечитает каталог.</summary>
    public void Invalidate() => Interlocked.Increment(ref generation);

    public async Task<string> ExpandAsync(string text, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TemplateAlias> aliases;
        try
        {
            // Запоминаем поколение ДО загрузки. Если Invalidate вызван во время I/O,
            // завершившаяся загрузка не погасит сброс: следующий вызов обновит снимок ещё раз.
            var current = Interlocked.Read(ref generation);
            if (snapshot is null || loadedGeneration != current || now() >= expires)
            {
                var loaded = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
                snapshot = loaded.ToArray();
                expires = now() + ttl;
                loadedGeneration = current;
            }
            aliases = snapshot;
        }
        finally { gate.Release(); }
        // Дальше работаем с полученным снимком вне блокировки, не задерживая другие сообщения.
        return TemplateAliases.Expand(text, aliases);
    }
}