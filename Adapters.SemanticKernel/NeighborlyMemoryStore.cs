﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Neighborly;

namespace NeighborlyMemory
{
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public class NeighborlyMemoryStore : IMemoryStore
    {
        private readonly VectorDatabase _vectorDatabase;

        public NeighborlyMemoryStore(VectorDatabase vectorDatabase)
        {
            _vectorDatabase = vectorDatabase;
        }

        public Task StoreAsync(string key, MemoryRecord record)
        {
            var vector = new Vector(record.Embedding.ToArray(), record.Metadata.Text);

            // Extract tags from the record's metadata and assign them to the vector
            if (record.Metadata.Description != null)
            {
                var tags = record.Metadata.Description.Split(',');
                foreach (var tag in tags)
                {
                    _vectorDatabase.Vectors.Tags.Add(tag);
                }

            }
            _vectorDatabase.Vectors.FirstOrDefault(vector);
            return Task.CompletedTask;
        }

        public Task<MemoryRecord> GetAsync(string key)
        {
            Guid guidKey;
            if (!Guid.TryParse(key, out guidKey))
            {
                throw new ArgumentException("Invalid Guid format", nameof(key));
            }

            var vector = _vectorDatabase.Vectors.Find(v => v.Id == guidKey);
            if (vector == null)
            {
                return Task.FromResult<MemoryRecord>(null);
            }

            var m = new MemoryRecordMetadata(true, vector.Id.ToString(), vector.OriginalText, string.Empty, string.Empty, string.Empty);
            var e = new ReadOnlyMemory<float>(vector.Values);
            var record = new MemoryRecord( metadata: m, 
                embedding: e,
                key:m.Id.ToString(),
                timestamp: null
                );

            return Task.FromResult(record);
        }

        public Task RemoveAsync(string key)
        {
            _vectorDatabase.Vectors.RemoveById(Guid.Parse(key));
            return Task.CompletedTask;
        }

        public Task<List<string>> GetKeysAsync()
        {
            var keys = _vectorDatabase.Vectors.Guids.Select(id => id.ToString()).ToList();
            return Task.FromResult(keys);
        }

        public Task<List<MemoryRecord>> GetVectorsAsync()
        {
            var vectors = _vectorDatabase.Vectors.GetAllVectors();
            var records = new List<MemoryRecord>();
            foreach (var vector in vectors)
            {
                var record = new MemoryRecord
                (
                    metadata: new MemoryRecordMetadata(true, vector.Id.ToString(), vector.OriginalText, string.Empty, string.Empty, string.Empty),
                    key: vector.Id.ToString(),
                    embedding: new ReadOnlyMemory<float>(vector.Values),
                    timestamp: null
                );
                records.Add(record);
            }
            return Task.FromResult(records);
        }

        public Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withEmbeddings = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName, ReadOnlyMemory<float> embedding, int limit, double minRelevanceScore = 0, bool withEmbeddings = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, ReadOnlyMemory<float> embedding, double minRelevanceScore = 0, bool withEmbedding = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

}