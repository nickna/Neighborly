using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neighborly.Search;

/// <summary>
/// Hierarchical Navigable Small World (HNSW) implementation for approximate nearest neighbor search
/// Based on the paper by Malkov and Yashunin: "Efficient and robust approximate nearest neighbor search using Hierarchical Navigable Small World graphs"
/// </summary>
public class HNSW
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private readonly HNSWConfig _config;
    private readonly Random _random;
    private readonly Dictionary<int, HNSWNode> _nodes;
    private int _nextNodeId;
    private int? _entryPointId;
    private int _maxLayer;

    /// <summary>
    /// Current configuration used by this HNSW instance
    /// </summary>
    public HNSWConfig Config => _config;

    /// <summary>
    /// Number of nodes in the graph
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// Maximum layer in the graph
    /// </summary>
    public int MaxLayer => _maxLayer;

    /// <summary>
    /// Entry point node ID (if any)
    /// </summary>
    public int? EntryPointId => _entryPointId;

    public HNSW() : this(new HNSWConfig())
    {
    }

    public HNSW(HNSWConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();
        
        _random = _config.Seed.HasValue ? new Random(_config.Seed.Value) : new Random();
        _nodes = new Dictionary<int, HNSWNode>();
        _nextNodeId = 0;
        _entryPointId = null;
        _maxLayer = 0;
    }

    /// <summary>
    /// Build HNSW graph from a list of vectors
    /// </summary>
    public void Build(VectorList vectors)
    {
        if (vectors == null)
            throw new ArgumentNullException(nameof(vectors));

        Clear();

        for (int i = 0; i < vectors.Count; i++)
        {
            var vector = vectors[i];
            if (vector != null)
            {
                Insert(vector);
            }
        }
    }

    /// <summary>
    /// Async version of Build that yields control periodically and supports cancellation
    /// </summary>
    public async Task BuildAsync(VectorList vectors, CancellationToken cancellationToken = default)
    {
        if (vectors == null)
            throw new ArgumentNullException(nameof(vectors));

        Clear();

        for (int i = 0; i < vectors.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var vector = vectors[i];
            if (vector != null)
            {
                Insert(vector);
                
                // Yield control every 10 insertions to prevent blocking
                if (i % 10 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    /// <summary>
    /// Clear all nodes and reset the graph
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _nextNodeId = 0;
        _entryPointId = null;
        _maxLayer = 0;
    }

    /// <summary>
    /// Insert a new vector into the HNSW graph
    /// </summary>
    public void Insert(Vector vector)
    {
        if (vector == null)
            throw new ArgumentNullException(nameof(vector));

        int nodeId = _nextNodeId++;
        int level = GetRandomLevel();
        var newNode = new HNSWNode(vector, nodeId, level);
        
        _nodes[nodeId] = newNode;

        // If this is the first node or it has a higher level than current entry point
        if (_entryPointId == null || level > _maxLayer)
        {
            _entryPointId = nodeId;
            _maxLayer = level;
        }

        // Search for closest nodes starting from entry point
        var entryPoint = _entryPointId.Value;
        var currentClosest = new List<int> { entryPoint };

        // Search from top layer down to level + 1
        for (int lc = _maxLayer; lc > level; lc--)
        {
            currentClosest = SearchLayer(vector, currentClosest, 1, lc);
        }

        // Search and connect from level down to 0
        for (int lc = Math.Min(level, _maxLayer); lc >= 0; lc--)
        {
            var candidates = SearchLayer(vector, currentClosest, _config.EfConstruction, lc);
            
            // Select neighbors and create connections
            int maxConnections = lc == 0 ? _config.MaxM0 : _config.M;
            var selectedNeighbors = SelectNeighbors(nodeId, candidates, maxConnections, lc);
            
            // Add connections
            foreach (var neighborId in selectedNeighbors)
            {
                newNode.AddConnection(neighborId, lc);
                _nodes[neighborId].AddConnection(nodeId, lc);
                
                // Prune connections if necessary
                PruneConnections(neighborId, lc);
            }
            
            currentClosest = selectedNeighbors;
        }
    }

    /// <summary>
    /// Search for k nearest neighbors
    /// </summary>
    public IList<Vector> Search(Vector query, int k, int? ef = null)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (k <= 0)
            throw new ArgumentException("k must be positive", nameof(k));
        if (_entryPointId == null)
            return new List<Vector>();

        int searchEf = ef ?? Math.Max(k, _config.Ef);
        var entryPoint = _entryPointId.Value;
        var currentClosest = new List<int> { entryPoint };

        // Search from top layer down to layer 1
        for (int lc = _maxLayer; lc > 0; lc--)
        {
            currentClosest = SearchLayer(query, currentClosest, 1, lc);
        }

        // Search layer 0 with ef
        var candidates = SearchLayer(query, currentClosest, searchEf, 0);
        
        // Return top k results
        return candidates
            .Take(k)
            .Select(nodeId => _nodes[nodeId].Vector)
            .ToList();
    }

    /// <summary>
    /// Generate random level for a new node using exponential decay
    /// </summary>
    private int GetRandomLevel()
    {
        int level = 0;
        while (_random.NextDouble() < (1.0 / _config.Ml) && level < 16) // Cap at 16 levels
        {
            level++;
        }
        return level;
    }

    /// <summary>
    /// Search for closest nodes in a specific layer
    /// </summary>
    private List<int> SearchLayer(Vector query, List<int> entryPoints, int numClosest, int layer)
    {
        var visited = new HashSet<int>();
        var candidates = new SortedSet<(float distance, int nodeId)>();
        var dynamicCandidates = new SortedSet<(float distance, int nodeId)>();

        // Initialize with entry points
        foreach (var entryId in entryPoints)
        {
            if (_nodes.ContainsKey(entryId) && _nodes[entryId].MaxLayer >= layer)
            {
                float distance = _nodes[entryId].Vector.Distance(query);
                candidates.Add((distance, entryId));
                dynamicCandidates.Add((distance, entryId));
                visited.Add(entryId);
            }
        }

        while (dynamicCandidates.Count > 0)
        {
            var current = dynamicCandidates.Min;
            dynamicCandidates.Remove(current);

            // Stop if we've found enough good candidates
            if (candidates.Count >= numClosest && current.distance > candidates.Max.distance)
                break;

            // Explore neighbors
            var connections = _nodes[current.nodeId].GetConnections(layer);
            foreach (var neighborId in connections)
            {
                if (!visited.Contains(neighborId) && _nodes.ContainsKey(neighborId))
                {
                    visited.Add(neighborId);
                    float distance = _nodes[neighborId].Vector.Distance(query);
                    
                    if (candidates.Count < numClosest || distance < candidates.Max.distance)
                    {
                        candidates.Add((distance, neighborId));
                        dynamicCandidates.Add((distance, neighborId));
                        
                        // Keep only best candidates
                        if (candidates.Count > numClosest)
                        {
                            candidates.Remove(candidates.Max);
                        }
                    }
                }
            }
        }

        return candidates.Select(c => c.nodeId).ToList();
    }

    /// <summary>
    /// Select best neighbors using simple heuristic
    /// </summary>
    private List<int> SelectNeighbors(int nodeId, List<int> candidates, int maxConnections, int layer)
    {
        var queryVector = _nodes[nodeId].Vector;
        
        // Sort candidates by distance to query
        var sortedCandidates = candidates
            .Where(id => _nodes.ContainsKey(id) && _nodes[id].MaxLayer >= layer)
            .Select(id => new { Id = id, Distance = _nodes[id].Vector.Distance(queryVector) })
            .OrderBy(x => x.Distance)
            .ToList();

        // Take best candidates up to maxConnections
        return sortedCandidates
            .Take(maxConnections)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// Prune connections of a node if it exceeds maximum allowed connections
    /// </summary>
    private void PruneConnections(int nodeId, int layer)
    {
        if (!_nodes.ContainsKey(nodeId))
            return;

        var node = _nodes[nodeId];
        var connections = node.GetConnections(layer);
        int maxConnections = layer == 0 ? _config.MaxM0 : _config.M;

        if (connections.Count <= maxConnections)
            return;

        // Select best connections to keep
        var connectionDistances = connections
            .Select(id => new { Id = id, Distance = node.Vector.Distance(_nodes[id].Vector) })
            .OrderBy(x => x.Distance)
            .ToList();

        var toKeep = connectionDistances.Take(maxConnections).Select(x => x.Id).ToHashSet();
        var toRemove = connections.Except(toKeep).ToList();

        // Remove excess connections
        foreach (var removeId in toRemove)
        {
            node.RemoveConnection(removeId, layer);
            if (_nodes.ContainsKey(removeId))
            {
                _nodes[removeId].RemoveConnection(nodeId, layer);
            }
        }
    }

    /// <summary>
    /// Save HNSW graph to binary stream
    /// </summary>
    public async Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));

        writer.Write(s_currentFileVersion);
        writer.Write(_nodes.Count);
        writer.Write(_maxLayer);
        writer.Write(_entryPointId ?? -1);

        // Write configuration
        writer.Write(_config.M);
        writer.Write(_config.MaxM0);
        writer.Write(_config.EfConstruction);
        writer.Write(_config.Ef);
        writer.Write(_config.Ml);

        // Write nodes
        foreach (var kvp in _nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteNodeAsync(writer, kvp.Value, cancellationToken);
        }
    }

    /// <summary>
    /// Load HNSW graph from binary stream
    /// </summary>
    public async Task LoadAsync(BinaryReader reader, VectorList vectors, CancellationToken cancellationToken = default)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));
        if (vectors == null)
            throw new ArgumentNullException(nameof(vectors));

        var version = reader.ReadInt32();
        if (version != s_currentFileVersion)
            throw new InvalidDataException($"Invalid HNSW version: {version}");

        Clear();

        int nodeCount = reader.ReadInt32();
        _maxLayer = reader.ReadInt32();
        int entryPointId = reader.ReadInt32();
        _entryPointId = entryPointId >= 0 ? entryPointId : null;

        // Read configuration (but don't override current config)
        reader.ReadInt32(); // M
        reader.ReadInt32(); // MaxM0  
        reader.ReadInt32(); // EfConstruction
        reader.ReadInt32(); // Ef
        reader.ReadDouble(); // Ml

        // Read nodes
        for (int i = 0; i < nodeCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReadNodeAsync(reader, vectors, cancellationToken);
        }

        _nextNodeId = _nodes.Count > 0 ? _nodes.Keys.Max() + 1 : 0;
    }

    private async Task WriteNodeAsync(BinaryWriter writer, HNSWNode node, CancellationToken cancellationToken)
    {
        writer.Write(node.Id);
        writer.Write(node.Vector.Id.ToByteArray());
        writer.Write(node.MaxLayer);

        // Write connections for each layer
        for (int layer = 0; layer <= node.MaxLayer; layer++)
        {
            var connections = node.GetConnections(layer);
            writer.Write(connections.Count);
            foreach (var connectionId in connections)
            {
                writer.Write(connectionId);
            }
        }

        await Task.Yield(); // Allow cancellation
    }

    private async Task ReadNodeAsync(BinaryReader reader, VectorList vectors, CancellationToken cancellationToken)
    {
        int nodeId = reader.ReadInt32();
        var vectorGuidBytes = reader.ReadBytes(16);
        var vectorGuid = new Guid(vectorGuidBytes);
        int maxLayer = reader.ReadInt32();

        // Find vector in the vector list
        var vector = vectors.GetById(vectorGuid);
        if (vector == null)
            return; // Skip if vector not found

        var node = new HNSWNode(vector, nodeId, maxLayer);

        // Read connections for each layer
        for (int layer = 0; layer <= maxLayer; layer++)
        {
            int connectionCount = reader.ReadInt32();
            for (int i = 0; i < connectionCount; i++)
            {
                int connectionId = reader.ReadInt32();
                node.AddConnection(connectionId, layer);
            }
        }

        _nodes[nodeId] = node;
        await Task.Yield(); // Allow cancellation
    }

    public override bool Equals(object? obj)
    {
        return obj is HNSW other && _nodes.Count == other._nodes.Count;
    }

    public override int GetHashCode()
    {
        return _nodes.Count.GetHashCode();
    }
}