using Neighborly;

namespace API.Services
{
    /// <summary>
    /// REST API endpoints for the Vector service
    /// </summary>
    public static class RestServices
    {
        public static void MapVectorRoutes(WebApplication app)
        {
            app.MapPost("/vector", (VectorDatabase db, Vector vector) =>
            {
                db.Vectors.Add(vector);
                return Results.Created($"/vectors/{vector.Id}", vector);
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
                    return Results.Ok(vector);
                }
            });

            app.MapPut("/vector/{id}", (VectorDatabase db, Vector vector) =>
            {
                if (db.Vectors.Update(vector))
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
                    db.Vectors.Add(vector);
                }

                return Results.NoContent();
            });

            app.MapGet("/vectors/searchNearest", (VectorDatabase db, Vector query, int k) =>
            {
                var vectors = db.Search(query, k);
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
}
