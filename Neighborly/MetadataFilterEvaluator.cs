using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Neighborly;

public static class MetadataFilterEvaluator
{
    /// <summary>
    /// Thread-safe cache for compiled regex patterns.
    /// Provides significant performance improvement for repeated regex evaluations.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex> s_regexCache = new();

    /// <summary>
    /// Maximum number of cached regex patterns to prevent unbounded memory growth.
    /// </summary>
    private const int MaxCachedPatterns = 1000;

    /// <summary>
    /// Frozen lookup for legacy field names with case-insensitive matching.
    /// Maps various field name variants to their canonical form.
    /// </summary>
    private static readonly FrozenDictionary<string, string> s_legacyFieldMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "tags",
            ["tag"] = "tags",
            ["userid"] = "userid",
            ["user_id"] = "userid",
            ["orgid"] = "orgid",
            ["org_id"] = "orgid",
            ["priority"] = "priority",
            ["originaltext"] = "originaltext",
            ["original_text"] = "originaltext",
            ["text"] = "originaltext",
            ["id"] = "id"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a predicate function from a MetadataFilter that can be used to filter vectors.
    /// </summary>
    /// <param name="filter">The metadata filter to convert to a predicate</param>
    /// <returns>A predicate function that evaluates a vector against the filter criteria</returns>
    public static Func<Vector, bool> CreatePredicate(MetadataFilter? filter)
    {
        if (filter == null || !filter.HasFilters)
            return static _ => true; // No filter means accept all - static lambda avoids allocation

        // Pre-allocate array to avoid LINQ allocation
        var filters = filter.Filters;
        var expressions = new Func<Vector, bool>[filters.Count];
        int index = 0;
        foreach (var kvp in filters)
        {
            expressions[index++] = CreateExpressionPredicate(kvp.Key, kvp.Value);
        }

        return filter.Logic == FilterLogic.And
            ? vector => EvaluateAll(expressions, vector)
            : vector => EvaluateAny(expressions, vector);
    }

    /// <summary>
    /// Evaluates all predicates against a vector (AND logic).
    /// Uses foreach loop to avoid LINQ All() allocation.
    /// </summary>
    private static bool EvaluateAll(Func<Vector, bool>[] predicates, Vector vector)
    {
        foreach (var predicate in predicates)
        {
            if (!predicate(vector))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Evaluates any predicate against a vector (OR logic).
    /// Uses foreach loop to avoid LINQ Any() allocation.
    /// </summary>
    private static bool EvaluateAny(Func<Vector, bool>[] predicates, Vector vector)
    {
        foreach (var predicate in predicates)
        {
            if (predicate(vector))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Creates a predicate function for a single filter expression.
    /// </summary>
    /// <param name="key">The metadata key to filter on</param>
    /// <param name="filterValue">The filter value and operator</param>
    /// <returns>A predicate function for the expression</returns>
    private static Func<Vector, bool> CreateExpressionPredicate(string key, FilterValue filterValue)
    {
        return vector =>
        {
            // Handle legacy fields that are not in metadata
            if (IsLegacyField(key))
            {
                return EvaluateLegacyField(vector, key, filterValue);
            }
            
            // Handle metadata fields
            if (!vector.Metadata.TryGetValue(key, out var metadataValue))
            {
                // Key doesn't exist in metadata
                return filterValue.Operator == FilterOperator.NotEquals || 
                       filterValue.Operator == FilterOperator.NotContains ||
                       filterValue.Operator == FilterOperator.NotIn;
            }
            
            return EvaluateExpression(metadataValue, filterValue);
        };
    }
    
    /// <summary>
    /// Checks if a key refers to a legacy field (Tags, Attributes, etc.) rather than metadata.
    /// Uses FrozenDictionary for O(1) case-insensitive lookup without string allocation.
    /// </summary>
    private static bool IsLegacyField(string key) => s_legacyFieldMappings.ContainsKey(key);
    
    /// <summary>
    /// Evaluates filter expressions against legacy Vector fields.
    /// Uses FrozenDictionary for normalized field name lookup without string allocation.
    /// </summary>
    private static bool EvaluateLegacyField(Vector vector, string key, FilterValue filterValue)
    {
        if (!s_legacyFieldMappings.TryGetValue(key, out var normalizedKey))
            return false;

        return normalizedKey switch
        {
            "tags" => EvaluateTagsField(vector.Tags, filterValue),
            "userid" => EvaluateExpression(vector.Attributes.UserId, filterValue),
            "orgid" => EvaluateExpression(vector.Attributes.OrgId, filterValue),
            "priority" => EvaluateExpression(vector.Attributes.Priority, filterValue),
            "originaltext" => EvaluateExpression(vector.OriginalText, filterValue),
            "id" => EvaluateExpression(vector.Id.ToString(), filterValue),
            _ => false
        };
    }
    
    /// <summary>
    /// Special handling for tags field which is an array of shorts.
    /// </summary>
    private static bool EvaluateTagsField(short[] tags, FilterValue filterValue)
    {
        return filterValue.Operator switch
        {
            FilterOperator.Equals => filterValue.Value switch
            {
                short shortVal => tags.Contains(shortVal),
                int intVal => tags.Contains((short)intVal),
                string strVal => short.TryParse(strVal, out var parsed) && tags.Contains(parsed),
                _ => false
            },
            FilterOperator.NotEquals => !EvaluateTagsField(tags, new FilterValue(filterValue.Value, FilterOperator.Equals)),
            FilterOperator.Contains => filterValue.Value switch
            {
                short shortVal => tags.Contains(shortVal),
                int intVal => tags.Contains((short)intVal),
                string strVal => short.TryParse(strVal, out var parsed) && tags.Contains(parsed),
                _ => false
            },
            FilterOperator.NotContains => !EvaluateTagsField(tags, new FilterValue(filterValue.Value, FilterOperator.Contains)),
            FilterOperator.In => filterValue.Value switch
            {
                short[] shortArray => shortArray.Any(tags.Contains),
                int[] intArray => intArray.Any(i => tags.Contains((short)i)),
                string[] strArray => strArray.Any(s => short.TryParse(s, out var parsed) && tags.Contains(parsed)),
                _ => false
            },
            FilterOperator.NotIn => !EvaluateTagsField(tags, new FilterValue(filterValue.Value, FilterOperator.In)),
            _ => false
        };
    }
    
    /// <summary>
    /// Evaluates a filter expression against a metadata value.
    /// </summary>
    private static bool EvaluateExpression(object metadataValue, FilterValue filterValue)
    {
        try
        {
            return filterValue.Operator switch
            {
                FilterOperator.Equals => AreEqual(metadataValue, filterValue.Value),
                FilterOperator.NotEquals => !AreEqual(metadataValue, filterValue.Value),
                FilterOperator.GreaterThan => IsGreaterThan(metadataValue, filterValue.Value),
                FilterOperator.LessThan => IsLessThan(metadataValue, filterValue.Value),
                FilterOperator.GreaterEqual => IsGreaterThan(metadataValue, filterValue.Value) || AreEqual(metadataValue, filterValue.Value),
                FilterOperator.LessEqual => IsLessThan(metadataValue, filterValue.Value) || AreEqual(metadataValue, filterValue.Value),
                FilterOperator.Contains => Contains(metadataValue, filterValue.Value),
                FilterOperator.NotContains => !Contains(metadataValue, filterValue.Value),
                FilterOperator.In => IsIn(metadataValue, filterValue.Value),
                FilterOperator.NotIn => !IsIn(metadataValue, filterValue.Value),
                FilterOperator.Regex => MatchesRegex(metadataValue, filterValue.Value),
                FilterOperator.StartsWith => StartsWith(metadataValue, filterValue.Value),
                FilterOperator.EndsWith => EndsWith(metadataValue, filterValue.Value),
                _ => false
            };
        }
        catch (InvalidCastException ex)
        {
            Logging.Logger.Debug(ex, "Type conversion failed during filter evaluation. MetadataType: {MetadataType}, FilterValueType: {FilterType}",
                metadataValue?.GetType().Name ?? "null",
                filterValue.Value?.GetType().Name ?? "null");
            return false;
        }
        catch (FormatException ex)
        {
            Logging.Logger.Debug(ex, "Format conversion failed during filter evaluation");
            return false;
        }
        catch (OverflowException ex)
        {
            Logging.Logger.Debug(ex, "Numeric overflow during filter evaluation");
            return false;
        }
    }
    
    private static bool AreEqual(object left, object right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        
        // Handle numeric comparisons with type coercion
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Math.Abs(Convert.ToDouble(left) - Convert.ToDouble(right)) < double.Epsilon;
        }
        
        // Handle string comparisons (case-insensitive)
        if (left is string leftStr && right is string rightStr)
        {
            return string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
        }
        
        // Handle DateTime comparisons
        if (left is DateTime leftDate && right is DateTime rightDate)
        {
            return leftDate == rightDate;
        }
        
        // Handle bool comparisons
        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }
        
        // Fall back to object equality
        return left.Equals(right);
    }
    
    private static bool IsGreaterThan(object left, object right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDouble(left) > Convert.ToDouble(right);
        }
        
        if (left is DateTime leftDate && right is DateTime rightDate)
        {
            return leftDate > rightDate;
        }
        
        if (left is string leftStr && right is string rightStr)
        {
            return string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) > 0;
        }
        
        return false;
    }
    
    private static bool IsLessThan(object left, object right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDouble(left) < Convert.ToDouble(right);
        }
        
        if (left is DateTime leftDate && right is DateTime rightDate)
        {
            return leftDate < rightDate;
        }
        
        if (left is string leftStr && right is string rightStr)
        {
            return string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) < 0;
        }
        
        return false;
    }
    
    private static bool Contains(object container, object value)
    {
        if (container is string containerStr && value is string valueStr)
        {
            return containerStr.Contains(valueStr, StringComparison.OrdinalIgnoreCase);
        }
        
        if (container is Array containerArray)
        {
            foreach (var item in containerArray)
            {
                if (AreEqual(item, value))
                    return true;
            }
        }
        
        return false;
    }
    
    private static bool IsIn(object value, object container)
    {
        if (container is Array containerArray)
        {
            foreach (var item in containerArray)
            {
                if (AreEqual(value, item))
                    return true;
            }
        }
        
        return false;
    }
    
    private static bool MatchesRegex(object text, object pattern)
    {
        if (text is not string textStr || pattern is not string patternStr)
            return false;

        try
        {
            // Get or create cached compiled regex
            var regex = s_regexCache.GetOrAdd(patternStr, p =>
            {
                // Prevent unbounded cache growth
                if (s_regexCache.Count >= MaxCachedPatterns)
                {
                    s_regexCache.Clear();
                }
                return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
            });

            return regex.IsMatch(textStr);
        }
        catch (RegexMatchTimeoutException ex)
        {
            Logging.Logger.Warning(ex, "Regex match timeout for pattern: {Pattern}", patternStr);
            return false;
        }
        catch (ArgumentException ex)
        {
            Logging.Logger.Warning(ex, "Invalid regex pattern: {Pattern}", patternStr);
            return false;
        }
    }
    
    private static bool StartsWith(object text, object prefix)
    {
        if (text is string textStr && prefix is string prefixStr)
        {
            return textStr.StartsWith(prefixStr, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
    
    private static bool EndsWith(object text, object suffix)
    {
        if (text is string textStr && suffix is string suffixStr)
        {
            return textStr.EndsWith(suffixStr, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
    
    private static bool IsNumeric(object value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}