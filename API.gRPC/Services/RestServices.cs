using Microsoft.AspNetCore.Mvc;
using Neighborly;
using Neighborly.API.Mappers;
using Neighborly.API.Models;
using Neighborly.Search;

namespace API.Services;

/// <summary>
/// REST API endpoints for the Vector service
/// </summary>
public static class RestServices
{
    public static void MapVectorRoutes(WebApplication app, VectorMapper vectorMapper)
    {
        app.MapPost("/vector", async (VectorDatabase db, VectorDto vector, HttpContext context) =>
        {
            db.Vectors.Add(vectorMapper.Map(vector));
            context.Response.StatusCode = 201;
            context.Response.Headers["Location"] = $"/vector/{vector.Id}";
            context.Response.ContentType = "application/json";
            var json = System.Text.Json.JsonSerializer.Serialize(vector);
            await context.Response.WriteAsync(json);
        });

        app.MapGet("/vector/{id}", async (VectorDatabase db, Guid id, HttpContext context) =>
        {
            var vector = db.Vectors.FirstOrDefault(v => v.Id == id);

            if (vector == null)
            {
                context.Response.StatusCode = 404;
            }
            else
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var json = System.Text.Json.JsonSerializer.Serialize(vectorMapper.Map(vector));
                await context.Response.WriteAsync(json);
            }
        });

        app.MapPut("/vector/{id}", async (VectorDatabase db, Guid Id, VectorDto vector, HttpContext context) =>
        {
            if (db.Vectors.Update(Id, vectorMapper.Map(vector)))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var json = System.Text.Json.JsonSerializer.Serialize(vector);
                await context.Response.WriteAsync(json);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        });

        app.MapDelete("/vector/{id}", (VectorDatabase db, Guid id, HttpContext context) =>
        {
            var vector = db.Vectors.FirstOrDefault(v => v.Id == id);

            if (vector == null)
            {
                context.Response.StatusCode = 404;
            }
            else
            {
                db.Vectors.RemoveById(id);
                context.Response.StatusCode = 204;
            }
        });

        app.MapPost("/vectors/searchNearest", async ([FromServices] VectorDatabase db, [FromBody] VectorDto query, [FromQuery] int k, HttpContext context) =>
        {
            var vectors = db.Search(vectorMapper.Map(query), k);
            if (vectors == null)
            {
                context.Response.StatusCode = 404;
            }
            else
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var json = System.Text.Json.JsonSerializer.Serialize(vectors);
                await context.Response.WriteAsync(json);
            }
        });

        app.MapPost("/vectors/searchWithMetadata", async ([FromServices] VectorDatabase db, [FromBody] SearchWithMetadataRequestDto request, HttpContext context) =>
        {
            try
            {
                var queryVector = vectorMapper.Map(request.Vector);
                var metadataFilter = ConvertToMetadataFilter(request.MetadataFilter);
                var algorithm = ConvertToSearchAlgorithm(request.Algorithm);
                
                if (metadataFilter == null)
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    var errorResponse = new { error = "MetadataFilter is required for this endpoint" };
                    var errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(errorJson);
                    return;
                }
                
                var vectors = db.SearchWithMetadata(
                    queryVector,
                    request.K,
                    metadataFilter,
                    algorithm,
                    request.SimilarityThreshold
                );

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var vectorDtos = vectors.Select(vectorMapper.Map).ToList();
                var json = System.Text.Json.JsonSerializer.Serialize(vectorDtos);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var errorResponse = new { error = ex.Message };
                var errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(errorJson);
            }
        });

        app.MapPost("/vectors/rangeSearch", async ([FromServices] VectorDatabase db, [FromBody] RangeSearchRequestDto request, HttpContext context) =>
        {
            try
            {
                var queryVector = vectorMapper.Map(request.Vector);
                var metadataFilter = ConvertToMetadataFilter(request.MetadataFilter);
                var algorithm = ConvertToSearchAlgorithm(request.Algorithm);
                
                var vectors = metadataFilter != null
                    ? db.RangeSearchWithMetadata(queryVector, request.Radius, metadataFilter, algorithm)
                    : db.RangeSearch(queryVector, request.Radius, algorithm);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var vectorDtos = vectors.Select(vectorMapper.Map).ToList();
                var json = System.Text.Json.JsonSerializer.Serialize(vectorDtos);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var errorResponse = new { error = ex.Message };
                var errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(errorJson);
            }
        });

        app.MapDelete("/db/clear", (VectorDatabase db, HttpContext context) =>
        {
            db.Vectors.Clear();
            context.Response.StatusCode = 204;
        });
    }

    private static MetadataFilter? ConvertToMetadataFilter(MetadataFilterDto? dto)
    {
        if (dto == null || dto.Expressions.Count == 0)
            return null;

        var metadataFilter = new MetadataFilter();
        
        // Convert logic
        metadataFilter.Logic = dto.Logic.ToLowerInvariant() == "or" ? FilterLogic.Or : FilterLogic.And;
        
        // Convert expressions
        foreach (var expr in dto.Expressions)
        {
            var filterOperator = ConvertToFilterOperator(expr.Operator);
            metadataFilter.Filters[expr.Key] = new FilterValue(expr.Value, filterOperator);
        }
        
        return metadataFilter;
    }

    private static FilterOperator ConvertToFilterOperator(string operatorString)
    {
        return operatorString.ToLowerInvariant() switch
        {
            "equals" => FilterOperator.Equals,
            "notequals" => FilterOperator.NotEquals,
            "greaterthan" => FilterOperator.GreaterThan,
            "lessthan" => FilterOperator.LessThan,
            "greaterequal" => FilterOperator.GreaterEqual,
            "lessequal" => FilterOperator.LessEqual,
            "contains" => FilterOperator.Contains,
            "notcontains" => FilterOperator.NotContains,
            "in" => FilterOperator.In,
            "notin" => FilterOperator.NotIn,
            "regex" => FilterOperator.Regex,
            "startswith" => FilterOperator.StartsWith,
            "endswith" => FilterOperator.EndsWith,
            _ => FilterOperator.Equals
        };
    }

    private static Neighborly.Search.SearchAlgorithm ConvertToSearchAlgorithm(string algorithm)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "kdtree" => Neighborly.Search.SearchAlgorithm.KDTree,
            "balltree" => Neighborly.Search.SearchAlgorithm.BallTree,
            "linear" => Neighborly.Search.SearchAlgorithm.Linear,
            "lsh" => Neighborly.Search.SearchAlgorithm.LSH,
            _ => Neighborly.Search.SearchAlgorithm.KDTree
        };
    }
}
