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
        Filters[key] = new FilterValue { Value = value, Operator = op };
    }
    
    public MetadataFilter(Dictionary<string, FilterValue> filters, FilterLogic logic = FilterLogic.And)
    {
        Filters = filters;
        Logic = logic;
    }
    
    public MetadataFilter Add(string key, object value, FilterOperator op = FilterOperator.Equals)
    {
        Filters[key] = new FilterValue { Value = value, Operator = op };
        return this;
    }
    
    public bool HasFilters => Filters.Count > 0;
}

public class FilterValue
{
    public object Value { get; set; } = null!;
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;
    
    public FilterValue()
    {
    }
    
    public FilterValue(object value, FilterOperator op = FilterOperator.Equals)
    {
        Value = value;
        Operator = op;
    }
}

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