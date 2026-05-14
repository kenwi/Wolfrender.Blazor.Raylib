using System.Globalization;
using System.Numerics;
using System.Reflection;

namespace Game.Console;

public sealed class RuntimeVariableAccessor : IConsoleVariableAccessor
{
    private readonly RuntimePathResolver _pathResolver = new();
    private readonly Dictionary<string, RootBinding> _roots;
    private readonly HashSet<string> _writablePaths;

    public RuntimeVariableAccessor(IEnumerable<RootBinding> roots, IEnumerable<string> writablePaths)
    {
        _roots = roots.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        _writablePaths = new HashSet<string>(writablePaths.Select(_pathResolver.Normalize), StringComparer.OrdinalIgnoreCase);
    }

    public bool CanHandlePath(string path)
    {
        if (!_pathResolver.TryParse(path, out var resolved, out _))
            return false;
        return _roots.ContainsKey(resolved.RootName);
    }

    public bool TryGetValue(string path, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (!_pathResolver.TryParse(path, out var resolved, out error))
            return false;
        if (!_roots.TryGetValue(resolved.RootName, out var root))
        {
            error = $"Unknown root '{resolved.RootName}'.";
            return false;
        }

        if (!root.TryResolve(resolved.Index, out var current, out error))
            return false;
        if (current == null)
        {
            error = $"Root '{resolved.RootName}' resolved to null.";
            return false;
        }

        for (int i = 0; i < resolved.Members.Count; i++)
        {
            var memberName = resolved.Members[i];
            if (!TryResolveMember(current!, memberName, out var memberInfo, out error))
                return false;

            current = GetMemberValue(current!, memberInfo);
            if (i < resolved.Members.Count - 1 && current == null)
            {
                error = $"Path segment '{memberName}' resolved to null.";
                return false;
            }
        }

        value = FormatValue(current);
        return true;
    }

    public bool TrySetValue(string path, string valueText, out string error)
    {
        error = string.Empty;

        if (!_pathResolver.TryParse(path, out var resolved, out error))
            return false;
        if (!_roots.TryGetValue(resolved.RootName, out var root))
        {
            error = $"Unknown root '{resolved.RootName}'.";
            return false;
        }
        if (!_writablePaths.Contains(resolved.NormalizedPath))
        {
            error = $"Path is read-only or not allowed: {path}";
            return false;
        }

        if (!root.TryResolve(resolved.Index, out var current, out error))
            return false;
        if (current == null)
        {
            error = $"Root '{resolved.RootName}' resolved to null.";
            return false;
        }

        for (int i = 0; i < resolved.Members.Count - 1; i++)
        {
            var memberName = resolved.Members[i];
            if (!TryResolveMember(current!, memberName, out var memberInfo, out error))
                return false;
            current = GetMemberValue(current!, memberInfo);
            if (current == null)
            {
                error = $"Path segment '{memberName}' resolved to null.";
                return false;
            }
        }

        var finalMemberName = resolved.Members[^1];
        if (!TryResolveMember(current, finalMemberName, out var finalMember, out error))
            return false;

        var targetType = GetMemberType(finalMember);
        if (!TryParseValue(valueText, targetType, out var parsedValue, out error))
            return false;

        if (!TrySetMemberValue(current, finalMember, parsedValue, out error))
            return false;

        return true;
    }

    public IReadOnlyList<string> ListVariables(bool includeAll)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _roots.Values)
        {
            foreach (var variable in root.CuratedVariables)
                variables.Add(variable);
        }

        if (includeAll)
        {
            foreach (var root in _roots.Values)
            {
                foreach (var variable in root.DiscoverVariables())
                    variables.Add(variable);
            }
        }

        return variables.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool TryResolveMember(object current, string memberName, out MemberInfo memberInfo, out string error)
    {
        var type = current.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance;

        var property = type.GetProperties(flags)
            .FirstOrDefault(p => string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase));
        if (property != null)
        {
            memberInfo = property;
            error = string.Empty;
            return true;
        }

        var field = type.GetFields(flags)
            .FirstOrDefault(f => string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
        if (field != null)
        {
            memberInfo = field;
            error = string.Empty;
            return true;
        }

        memberInfo = null!;
        error = $"Member '{memberName}' was not found on '{type.Name}'.";
        return false;
    }

    private static object? GetMemberValue(object instance, MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.GetValue(instance),
            FieldInfo f => f.GetValue(instance),
            _ => null
        };
    }

    private static bool TrySetMemberValue(object instance, MemberInfo memberInfo, object value, out string error)
    {
        switch (memberInfo)
        {
            case PropertyInfo property:
                if (!property.CanWrite)
                {
                    error = $"Property '{property.Name}' is read-only.";
                    return false;
                }

                property.SetValue(instance, value);
                error = string.Empty;
                return true;

            case FieldInfo field:
                if (field.IsInitOnly)
                {
                    error = $"Field '{field.Name}' is read-only.";
                    return false;
                }

                field.SetValue(instance, value);
                error = string.Empty;
                return true;

            default:
                error = $"Unsupported member type: {memberInfo.MemberType}.";
                return false;
        }
    }

    private static Type GetMemberType(MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => typeof(string)
        };
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return "null";

        return value switch
        {
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            Vector2 v2 => $"{v2.X.ToString("0.###", CultureInfo.InvariantCulture)},{v2.Y.ToString("0.###", CultureInfo.InvariantCulture)}",
            Vector3 v3 => $"{v3.X.ToString("0.###", CultureInfo.InvariantCulture)},{v3.Y.ToString("0.###", CultureInfo.InvariantCulture)},{v3.Z.ToString("0.###", CultureInfo.InvariantCulture)}",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool TryParseValue(string text, Type targetType, out object value, out string error)
    {
        error = string.Empty;
        value = string.Empty;

        var isNullable = Nullable.GetUnderlyingType(targetType) != null;
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (isNullable && string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
        {
            value = null!;
            return true;
        }

        if (effectiveType == typeof(string))
        {
            value = text;
            return true;
        }
        if (effectiveType == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            value = i;
            return true;
        }
        if (effectiveType == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
        {
            value = f;
            return true;
        }
        if (effectiveType == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            value = d;
            return true;
        }
        if (effectiveType == typeof(bool) && bool.TryParse(text, out var b))
        {
            value = b;
            return true;
        }
        if (effectiveType == typeof(Vector2) && TryParseVector2(text, out var v2))
        {
            value = v2;
            return true;
        }
        if (effectiveType == typeof(Vector3) && TryParseVector3(text, out var v3))
        {
            value = v3;
            return true;
        }
        if (effectiveType.IsEnum && Enum.TryParse(effectiveType, text, ignoreCase: true, out var enumValue))
        {
            value = enumValue!;
            return true;
        }

        error = $"Cannot parse '{text}' as {effectiveType.Name}.";
        return false;
    }

    private static bool TryParseVector2(string text, out Vector2 value)
    {
        value = default;
        var parts = NormalizeVectorText(text).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return false;

        value = new Vector2(x, y);
        return true;
    }

    private static bool TryParseVector3(string text, out Vector3 value)
    {
        value = default;
        var parts = NormalizeVectorText(text).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return false;

        value = new Vector3(x, y, z);
        return true;
    }

    private static string NormalizeVectorText(string text)
    {
        return text.Trim().TrimStart('(').TrimEnd(')');
    }
}

public sealed class RootBinding
{
    public required string Name { get; init; }
    public required Func<int?, ResolveResult> Resolver { get; init; }
    public required Func<IReadOnlyList<string>> CuratedListFactory { get; init; }
    public required Func<IReadOnlyList<string>> DiscoveryFactory { get; init; }

    public IEnumerable<string> CuratedVariables => CuratedListFactory();

    public ResolveResult Resolve(int? index) => Resolver(index);

    public bool TryResolve(int? index, out object target, out string error)
    {
        var result = Resolve(index);
        target = result.Target!;
        error = result.Error;
        return result.Success;
    }

    public IReadOnlyList<string> DiscoverVariables() => DiscoveryFactory();
}

public readonly record struct ResolveResult(bool Success, object? Target, string Error)
{
    public static ResolveResult Ok(object target) => new(true, target, string.Empty);
    public static ResolveResult Fail(string error) => new(false, null, error);
}
