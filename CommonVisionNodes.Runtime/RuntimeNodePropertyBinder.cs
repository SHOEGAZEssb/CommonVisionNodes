using System.Globalization;
using System.Reflection;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodes.Runtime;

public static class RuntimeNodePropertyBinder
{
    public static void Apply(Node node, IEnumerable<NodePropertyDto> properties)
    {
        var propertyMap = properties
            .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var property in GetBindableProperties(node.GetType()))
        {
            if (!propertyMap.TryGetValue(property.Name, out var rawValue))
                continue;

            var converted = ConvertFromString(rawValue, property.PropertyType);
            if (converted is null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
                continue;

            property.SetValue(node, converted);
        }
    }

    private static IEnumerable<PropertyInfo> GetBindableProperties(Type nodeType)
        => nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.PropertyType != typeof(Port)
                && property.CanWrite
                && property.GetSetMethod() is not null);

    private static object? ConvertFromString(string? value, Type targetType)
    {
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (actualType == typeof(string))
            return value;

        if (string.IsNullOrWhiteSpace(value))
            return Nullable.GetUnderlyingType(targetType) is not null ? null : null;

        if (actualType.IsEnum)
            return Enum.TryParse(actualType, value, ignoreCase: true, out var enumValue) ? enumValue : null;

        if (actualType == typeof(int))
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

        if (actualType == typeof(long))
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

        if (actualType == typeof(float))
            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) ? result : null;

        if (actualType == typeof(double))
            return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) ? result : null;

        if (actualType == typeof(bool))
            return bool.TryParse(value, out var result) ? result : null;

        return null;
    }
}
