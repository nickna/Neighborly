using System.Diagnostics.CodeAnalysis;
using Neighborly.API.Models;

namespace Neighborly.API.Mappers;

public sealed class VectorMapper
{
    public VectorDto? Map(Vector? vector)
    {
        if (vector == null)
        {
            return null;
        }

        return new VectorDto(
            vector.Id,
            vector.Values,
            vector.Tags,
            vector.OriginalText
        );
    }

    [return: NotNullIfNotNull(nameof(vectorDto))]
    public Vector? Map(VectorDto? vectorDto)
    {
        if (vectorDto == null)
        {
            return null;
        }

        return new Vector(
            vectorDto.Id,
            vectorDto.Values,
            vectorDto.Tags,
            vectorDto.OriginalText
        );
    }
}