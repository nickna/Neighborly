using System.Collections.Frozen;
using System.Collections.Generic;

namespace Neighborly;

public class MetadataFilter
{
    public Dictionary<string, FilterValue> Filters { get; set; } = new();
    public FilterLogic Logic { get; set; } = FilterLogic.And;
    
    public MetadataFilter()
    {
    }
    
    public MetadataFilter(string key, object value, FilterOperator op = FilterOperator.Equals)
    {
        Filters[key] = new FilterValue(value, op);
    }
    
    public MetadataFilter(Dictionary<string, FilterValue> filters, FilterLogic logic = FilterLogic.And)
    {
        Filters = filters;
        Logic = logic;
    }
    
    public MetadataFilter Add(string key, object value, FilterOperator op = FilterOperator.Equals)
    {
        Filters[key] = new FilterValue(value, op);
        return this;
    }

    public bool HasFilters => Filters.Count > 0;

    /// <summary>
    /// Creates a frozen (immutable, read-optimized) copy of the current filters.
    /// Use this when the filter will be reused many times without modification.
    /// </summary>
    public FrozenDictionary<string, FilterValue> ToFrozenFilters() => Filters.ToFrozenDictionary();
}

/// <summary>
/// Immutable value type representing a filter condition with a value and operator.
/// </summary>
/// <param name="Value">The value to filter against.</param>
/// <param name="Operator">The filter operator to apply.</param>
public readonly record struct FilterValue(object? Value, FilterOperator Operator = FilterOperator.Equals);

public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterEqual,
    LessEqual,
    Contains,
    NotContains,
    In,
    NotIn,
    Regex,
    StartsWith,
    EndsWith
}

public enum FilterLogic
{
    And,
    Or
}