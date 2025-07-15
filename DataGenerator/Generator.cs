using System.Collections;
using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace DataGenerator;

public class GeneratorConfig
{
    public Random? Random { get; set; }
    public IServiceProvider? ServiceProvider { get; set; }
}

public class Generator
{
    private readonly Dictionary<string, Func<object>> _rules = new();
    private readonly Random _random;
    private readonly IServiceProvider? _serviceProvider;
    private readonly Dictionary<string, int> _listSizes;

    public Generator(GeneratorConfig? generatorConfig = null)
    {
        generatorConfig ??= new GeneratorConfig();
        _random = generatorConfig.Random ?? Random.Shared;
        _serviceProvider = generatorConfig.ServiceProvider;
        _listSizes = [];
    }

    public T Generate<T>()
    {
        return (T)GenerateObject(typeof(T))!;
    }

    public List<T> GenerateList<T>(int size)
    {
        var list = new List<T>(size);
        for (int i = 0; i < size; i++)
        {
            list.Add((T)GenerateObject(typeof(T))!);
        }

        return list;
    }

    private object? GenerateObject(Type type, int preferredCollectionSize = -1)
    {
        if (_rules.TryGetValue(type.FullName!, out var typeRule)) return typeRule();
        if (_serviceProvider?.GetService(type) is { } resolvedProp) return resolvedProp;
        if (type.IsGenericType && type.GetGenericArguments().Length == 1 && TrytoPopulateAsList(type, preferredCollectionSize) is { } listResult) return listResult;

        object? obj = Activator.CreateInstance(type);
        if (obj is null) return null;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            SetProperty(obj, prop);
        }

        return obj;
    }

    private object? TrytoPopulateAsList(Type type, int preferredCollectionSize)
    {
        try
        {
            var genericType = type.GetGenericTypeDefinition();

            var testType = typeof(List<int>);
            var constructed = genericType.MakeGenericType(typeof(int));
            if (constructed.IsAssignableFrom(testType))
            {
                var innerType = type.GetGenericArguments()[0];

                var size = preferredCollectionSize == -1 ? _random.Next(1, 10) : preferredCollectionSize;

                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(innerType))!;

                for (int i = 0; i < size; i++)
                {
                    list.Add(GenerateObject(innerType));
                }
                return list;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void SetProperty(object obj, PropertyInfo prop)
    {
        if (!prop.CanWrite) 
        {
            return;
        }
        else if (_rules.TryGetValue(GetPropKey(prop), out var propRule))
        {
            prop.SetValue(obj, propRule());
        }
        else
        {
            var preferredSize = _listSizes.GetValueOrDefault(GetPropKey(prop), -1);
            prop.SetValue(obj, GenerateObject(prop.PropertyType, preferredSize));
        }
    }

    private static string GetPropKey(PropertyInfo prop) => $"{prop.DeclaringType.FullName}/{prop.Name}";

    internal void AddRule(string fullName, Func<object> rule)
    {
        _rules[fullName] = rule;
    }

    public Generator SetListSize<TType, TProp>(Expression<Func<TType, IEnumerable<TProp>>> getter, int size)
    {
        if (getter.Body is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                _listSizes[GetPropKey(propInfo)] = size;
                return this;
            }
        }

        throw new ArgumentException("Expression refers to a field, not a property.");
    }

}

public static class RuleExtensions
{
    public static Generator AddRule<TType>(this Generator generator, Func<TType> rule)
        where TType : notnull
    {
        generator.AddRule(typeof(TType).FullName!, () => rule());
        return generator;
    }

    public static Generator AddRule<TType>(this Generator generator, TType value)
        where TType : notnull
    {
        generator.AddRule(typeof(TType).FullName!, () => value);
        return generator;
    }

    public static Generator AddRule<TType, TProp>(this Generator generator, Expression<Func<TType, TProp>> getter, Func<TProp> rule)
        where TProp : notnull
    {
        if (getter.Body is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                generator.AddRule(GetPropKey(propInfo), () => rule());
                return generator;
            }
        }

        throw new ArgumentException("Expression refers to a field, not a property.");
    }

    public static Generator AddRule<TType, TProp>(this Generator generator, Expression<Func<TType, TProp>> getter, TProp value)
        where TProp : notnull
    {
        if (getter.Body is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                generator.AddRule(GetPropKey(propInfo), () => value);
                return generator;
            }
        }

        throw new ArgumentException("Expression refers to a field, not a property.");
    }

    private static string GetPropKey(PropertyInfo prop) => $"{prop.DeclaringType.FullName}/{prop.Name}";
}