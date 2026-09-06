namespace Main;

public static class GenericNames
{
    public static T Create<T>() where T : new() => new T();
    public static T Create<T>(T seed) => seed;
    public static TOut Convert<TIn, TOut>(TIn value, Func<TIn, TOut> convert) => convert(value);
}

public static class GenericNameExtensions
{
    public static T As<T>(this object value) where T : class => (T)value;
}

public class GenericNameUsage
{
    private readonly List<int> _items = new List<int>();
    private readonly Dictionary<string, List<int>> _index = new Dictionary<string, List<int>>();

    public void Invoke()
    {
        var created = GenericNames.Create<GenericNameUsage>();
        var seeded = GenericNames.Create<int>(1);
        var converted = GenericNames.Convert<int, string>(seeded, value => value.ToString());
        object boxed = this;
        var unboxed = boxed.As<GenericNameUsage>();
        var ints = _items.OfType<int>().ToList();
        var unbound = typeof(List<>);
        IEnumerable<int> sequence = _items;
        _index[converted] = ints;
    }
}
