using Microsoft.AspNetCore.Mvc;
using Neighborly;
using Neighborly.API.Mappers;
using Neighborly.API.Models;

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

        app.MapDelete("/db/clear", (VectorDatabase db, HttpContext context) =>
        {
            db.Vectors.Clear();
            context.Response.StatusCode = 204;
        });
    }
}
