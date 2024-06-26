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
        app.MapPost("/vector", (VectorDatabase db, VectorDto vector) =>
        {
            db.Vectors.Add(vectorMapper.Map(vector));
            return Results.Created($"/vector/{vector.Id}", vector);
        });

        app.MapGet("/vector/{id}", (VectorDatabase db, Guid id) =>
        {
            var vector = db.Vectors.FirstOrDefault(v => v.Id == id);

            if (vector == null)
            {
                return Results.NotFound();
            }
            else
            {
                return Results.Ok(vectorMapper.Map(vector));
            }
        });

        app.MapPut("/vector/{id}", (VectorDatabase db, Guid Id, VectorDto vector) =>
        {
            if (db.Vectors.Update(Id, vectorMapper.Map(vector)))
            {
                return Results.Ok(vector);
            }
            else
            {
                return Results.NotFound();
            }
        });

        app.MapDelete("/vector/{id}", (VectorDatabase db, Guid id) =>
        {
            var vector = db.Vectors.FirstOrDefault(v => v.Id == id);

            if (vector == null)
            {
                return Results.NotFound();
            }
            else
            {
                db.Vectors.RemoveById(id);
            }

            return Results.NoContent();
        });

        app.MapPost("/vectors/searchNearest", ([FromServices] VectorDatabase db, [FromBody] VectorDto query, [FromQuery] int k) =>
        {
            var vectors = db.Search(vectorMapper.Map(query), k);
            if (vectors == null)
            {
                return Results.NotFound();
            }
            else
            {
                return Results.Ok(vectors);
            }
        });

        app.MapDelete("/db/clear", (VectorDatabase db) =>
        {
            db.Vectors.Clear();
            return Results.NoContent();
        });
    }
}
