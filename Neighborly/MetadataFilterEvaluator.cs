using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neighborly;

public static class MetadataFilterEvaluator
{
    /// <summary>
    /// Creates a predicate function from a MetadataFilter that can be used to filter vectors.
    /// </summary>
    /// <param name="filter">The metadata filter to convert to a predicate</param>
    /// <returns>A predicate function that evaluates a vector against the filter criteria</returns>
    public static Func<Vector, bool> CreatePredicate(MetadataFilter? filter)
    {
        if (filter == null || !filter.HasFilters)
            return _ => true; // No filter means accept all
        
        var expressions = filter.Filters.Select(kvp => 
            CreateExpressionPredicate(kvp.Key, kvp.Value)).ToArray();
        
        return filter.Logic == FilterLogic.And
            ? vector => expressions.All(predicate => predicate(vector))
            : vector => expressions.Any(predicate => predicate(vector));
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
    /// </summary>
    private static bool IsLegacyField(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "tags" or "tag" => true,
            "userid" or "user_id" => true,
            "orgid" or "org_id" => true,
            "priority" => true,
            "originaltext" or "original_text" or "text" => true,
            "id" => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Evaluates filter expressions against legacy Vector fields.
    /// </summary>
    private static bool EvaluateLegacyField(Vector vector, string key, FilterValue filterValue)
    {
        return key.ToLowerInvariant() switch
        {
            "tags" or "tag" => EvaluateTagsField(vector.Tags, filterValue),
            "userid" or "user_id" => EvaluateExpression(vector.Attributes.UserId, filterValue),
            "orgid" or "org_id" => EvaluateExpression(vector.Attributes.OrgId, filterValue),
            "priority" => EvaluateExpression(vector.Attributes.Priority, filterValue),
            "originaltext" or "original_text" or "text" => EvaluateExpression(vector.OriginalText, filterValue),
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
        catch
        {
            // If evaluation fails (e.g., type conversion), return false
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
        if (text is string textStr && pattern is string patternStr)
        {
            try
            {
                return Regex.IsMatch(textStr, patternStr, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        return false;
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