namespace Neighborly.Persistence;

/// <summary>
/// Result of a database load operation.
/// </summary>
/// <param name="VectorCount">The number of vectors loaded from the file.</param>
/// <param name="IndexesNeedRebuild">Whether search indexes need to be rebuilt after loading.</param>
internal readonly record struct LoadResult(int VectorCount, bool IndexesNeedRebuild);
