using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Neighborly.API.Models;

/// <summary>
/// Data transfer object for metadata filtering in REST API
/// </summary>
public class MetadataFilterDto
{
    [JsonPropertyName("logic")]
    public string Logic { get; set; } = "and";
    
    [JsonPropertyName("expressions")]
    public List<FilterExpressionDto> Expressions { get; set; } = new();
}

/// <summary>
/// Data transfer object for filter expressions
/// </summary>
public class FilterExpressionDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public object Value { get; set; } = null!;
    
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";
}

/// <summary>
/// Data transfer object for search requests with metadata filtering
/// </summary>
public class SearchWithMetadataRequestDto
{
    [JsonPropertyName("vector")]
    public VectorDto Vector { get; set; } = null!;
    
    [JsonPropertyName("k")]
    public int K { get; set; }
    
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "kdtree";
    
    [JsonPropertyName("similarity_threshold")]
    public float SimilarityThreshold { get; set; } = 0.5f;
    
    [JsonPropertyName("metadata_filter")]
    public MetadataFilterDto? MetadataFilter { get; set; }
}

/// <summary>
/// Data transfer object for range search requests with metadata filtering
/// </summary>
public class RangeSearchRequestDto
{
    [JsonPropertyName("vector")]
    public VectorDto Vector { get; set; } = null!;
    
    [JsonPropertyName("radius")]
    public float Radius { get; set; }
    
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "linear";
    
    [JsonPropertyName("metadata_filter")]
    public MetadataFilterDto? MetadataFilter { get; set; }
}