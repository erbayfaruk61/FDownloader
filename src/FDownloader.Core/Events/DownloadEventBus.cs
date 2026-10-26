namespace FDownloader.Core.Events;

/// <summary>
/// Bileşenler arası olay yayını (publish/subscribe).
/// Motor, depolama, ağ ve arayüz katmanları bu bus üzerinden haberleşir.
/// Doğrudan referans yerine olay kullanmak katmanları birbirinden ayırır.
/// </summary>
public sealed class DownloadEventBus
{
    // Olay tipi → abone listesi
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Belirli bir olay tipine abone ol.
    /// </summary>
    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        lock (_lock)
        {
            var type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out var list))
                _handlers[type] = list = new();
            list.Add(handler);
        }
    }

    /// <summary>
    /// Aboneliği iptal et.
    /// </summary>
    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        lock (_lock)
        {
            var type = typeof(TEvent);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }
    }

    /// <summary>
    /// Bir olay yayınla. Tüm aboneler çağrılır.
    /// Abonelerin hatası diğerlerini etkilemez.
    /// </summary>
    public void Publish<TEvent>(TEvent evt)
    {
        List<Delegate>? handlers;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out handlers) || handlers.Count == 0)
                return;
            handlers = new List<Delegate>(handlers); // Snapshot al, lock dışında çağır
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<TEvent>)handler)(evt);
            }
            catch
            {
                // Bir abonenin patlaması diğerlerini etkilemez
                // Logging burada olabilir ama circular dependency'den kaçınmak için şimdilik yok
            }
        }
    }

    /// <summary>
    /// Tüm abonelikleri temizle (uygulama kapanırken).
    /// </summary>
    public void Clear()
    {
        lock (_lock) _handlers.Clear();
    }
}
