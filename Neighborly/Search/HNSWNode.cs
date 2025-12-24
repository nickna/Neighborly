using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neighborly.Search;

/// <summary>
/// Represents a node in the HNSW (Hierarchical Navigable Small World) graph.
/// Sealed class with required properties for better type safety.
/// </summary>
public sealed class HNSWNode : IEquatable<HNSWNode>
{
    /// <summary>
    /// The vector data associated with this node
    /// </summary>
    public required Vector Vector { get; set; }

    /// <summary>
    /// Node ID for fast lookups and serialization
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    /// The maximum layer this node appears in (0-based)
    /// </summary>
    public required int MaxLayer { get; set; }

    /// <summary>
    /// Connections per layer. Index represents layer level.
    /// Each layer contains a list of connected node IDs.
    /// </summary>
    public required List<HashSet<int>> Connections { get; set; }

    [SetsRequiredMembers]
    public HNSWNode(Vector vector, int id, int maxLayer)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        Id = id;
        MaxLayer = maxLayer;
        Connections = new List<HashSet<int>>();

        // Initialize connection sets for each layer (0 to maxLayer)
        for (int i = 0; i <= maxLayer; i++)
        {
            Connections.Add(new HashSet<int>());
        }
    }

    /// <summary>
    /// Add a connection to another node at the specified layer
    /// </summary>
    public void AddConnection(int nodeId, int layer)
    {
        if (layer < 0 || layer > MaxLayer)
            return;
            
        Connections[layer].Add(nodeId);
    }

    /// <summary>
    /// Remove a connection to another node at the specified layer
    /// </summary>
    public void RemoveConnection(int nodeId, int layer)
    {
        if (layer < 0 || layer > MaxLayer)
            return;
            
        Connections[layer].Remove(nodeId);
    }

    /// <summary>
    /// Get all connections at the specified layer
    /// </summary>
    public HashSet<int> GetConnections(int layer)
    {
        if (layer < 0 || layer > MaxLayer)
            return [];

        return Connections[layer];
    }

    /// <summary>
    /// Calculate distance to another node's vector
    /// </summary>
    public float DistanceTo(HNSWNode other)
    {
        return Vector.Distance(other.Vector);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HNSWNode);
    }

    public bool Equals(HNSWNode? other)
    {
        return other != null && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(HNSWNode? left, HNSWNode? right)
    {
        return EqualityComparer<HNSWNode>.Default.Equals(left, right);
    }

    public static bool operator !=(HNSWNode? left, HNSWNode? right)
    {
        return !(left == right);
    }
}