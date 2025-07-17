using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace DataGenerator;

public class GeneratorConfig
{
    public Random? Random { get; set; }
}

public class Generator
{
    private readonly Dictionary<string, Func<Generator, object?>> _rules = new();
    private readonly Random _random;

    public Generator(GeneratorConfig? generatorConfig = null)
    {
        generatorConfig ??= new GeneratorConfig();
        _random = generatorConfig.Random ?? Random.Shared;
    }

    public T Generate<T>()
    {
        var stack = new HashSet<Type>();
        return (T)GenerateObject(typeof(T), stack)!;
    }

    public List<T> GenerateList<T>(int size)
    {
        var list = new List<T>(size);
        var stack = new HashSet<Type>();
        for (int i = 0; i < size; i++)
        {
            list.Add((T)GenerateObject(typeof(T), stack)!);
        }

        return list;
    }

    private object? GenerateObject(Type type, HashSet<Type> stack)
    {
        if (Nullable.GetUnderlyingType(type) is Type underlying) type = underlying;

        if (stack.Contains(type))
        {
            return GetDefault(type);
        }

        stack.Add(type);

        try
        {
            if (_rules.TryGetValue(type.FullName!, out var typeRule)) return typeRule(this);
            if (type.IsGenericType && type.GetGenericArguments().Length == 1 && TrytoPopulateAsList(type, stack) is { } listResult) return listResult;
            if (TryToPopulateAsComplexObject(type, stack, out var obj)) return obj;

            return GetDefault(type);
        }
        finally
        {
            stack.Remove(type);
        }
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private bool TryToPopulateAsComplexObject(Type type, HashSet<Type> stack, out object? value)
    {
        value = null;

        try
        {
            value = Activator.CreateInstance(type);
            if (value is null) return true;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                SetProperty(value, prop, stack);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private object? TrytoPopulateAsList(Type type, HashSet<Type> stack)
    {
        try
        {
            var genericType = type.GetGenericTypeDefinition();

            var testType = typeof(List<int>);
            var constructed = genericType.MakeGenericType(typeof(int));
            if (constructed.IsAssignableFrom(testType))
            {
                var innerType = type.GetGenericArguments()[0];

                var size = _random.Next(1, 10);

                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(innerType))!;

                for (int i = 0; i < size; i++)
                {
                    list.Add(GenerateObject(innerType, stack));
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

    private void SetProperty(object obj, PropertyInfo prop, HashSet<Type> stack)
    {
        if (!prop.CanWrite) 
        {
            return;
        }
        else if (_rules.TryGetValue(GetPropKey(prop), out var propRule))
        {
            prop.SetValue(obj, propRule(this));
        }
        else
        {
            prop.SetValue(obj, GenerateObject(prop.PropertyType, stack));
        }
    }

    private static string GetPropKey(PropertyInfo prop) => $"{prop.DeclaringType.FullName}/{prop.Name}";

    internal void AddRule(string fullName, Func<Generator, object?> rule)
    {
        _rules[fullName] = rule;
    }
}

public static class RuleExtensions
{
    public static Generator AddRule<TType>(this Generator generator, Func<Generator, TType?> rule)
    {
        generator.AddRule(typeof(TType).FullName!, g => rule(g));
        return generator;
    }

    public static Generator AddRule<TType>(this Generator generator, Func<TType?> rule)
    {
        generator.AddRule(typeof(TType).FullName!, g => rule());
        return generator;
    }

    public static Generator AddRule<TType>(this Generator generator, TType? value)
    {
        generator.AddRule(g => value);
        return generator;
    }

    public static Generator AddRule<TType, TProp>(this Generator generator, Expression<Func<TType, TProp>> getter, Func<Generator, TProp?> rule)
    {
        if (getter.Body is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                generator.AddRule($"{propInfo.DeclaringType!.FullName}/{propInfo.Name}", (g) => rule(g));
                return generator;
            }
        }

        throw new ArgumentException("Expression refers to a field, not a property.");
    }

    public static Generator AddRule<TType, TProp>(this Generator generator, Expression<Func<TType, TProp>> getter, TProp? value)
    {
        generator.AddRule(getter, g => value);
        return generator;
    }

    public static Generator AddRule<TType, TProp>(this Generator generator, Expression<Func<TType, TProp>> getter, Func<TProp?> rule)
    {
        generator.AddRule(getter, g => rule());
        return generator;
    }
}