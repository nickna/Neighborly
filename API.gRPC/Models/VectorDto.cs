namespace Neighborly.API.Models;

public record VectorDto(Guid Id, float[] Values, short[] Tags, string? OriginalText);