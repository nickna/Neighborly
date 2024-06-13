using Grpc.Core;
using Microsoft.Extensions.Logging;
using Neighborly;
using System.Threading.Tasks;
using Neighborly.API.Protos;

namespace Neighborly.API
{
    public class VectorService : Protos.Vector.VectorBase
    {
        private readonly ILogger<VectorService> _logger;
        private readonly VectorDatabase _db;
        public VectorService(ILogger<VectorService> logger, VectorDatabase db)
        {
            _logger = logger;
            _db = db;
        }

        public override Task<GetVectorsResponse> GetVectors(GetVectorsRequest request, ServerCallContext context)
        {
            // Create a new GetVectorsResponse
            var response = new GetVectorsResponse();

            // Get the vectors from the database
            var vectors = _db.Vectors;

            // Convert each Vector to a VectorMessage and add it to the response
            foreach (var vector in vectors)
            {
                var vectorMessage = new VectorMessage
                {
                    Values = Google.Protobuf.ByteString.CopyFrom(vector.ToBinary())
                };
                response.Vectors.Add(vectorMessage);
            }

            return Task.FromResult(response);
        }

        public override Task<GetVectorResponse> GetVectorById(GetVectorByIdRequest request, ServerCallContext context)
        {
            var vector = _db.Vectors.Find(v => v.Id == Guid.Parse(request.Id));
            if (vector != null)
            {
                var response = new GetVectorResponse
                {
                    Vector = Utility.ConvertToVectorMessage(vector)
                };
                return Task.FromResult(response);
            }
            else
            {
                return Task.FromResult(new GetVectorResponse());
            }
            
        }

        public override Task<Response> UpdateVector(UpdateVectorRequest request, ServerCallContext context)
        {
            // Convert the VectorMessage from the request to a Vector
            var newVector = Utility.ConvertToVector(request.Vector);

            // Update the vector in the database
            var success = _db.Vectors.Update(Guid.Parse(request.Id), newVector);

            // Create the response
            var response = new Response { Success = success };

            return Task.FromResult(response);
        }



        public override Task<SearchResponse> SearchNearest(SearchNearestRequest request, ServerCallContext context)
        {
            var query = Utility.ConvertToVector(request.Query);
            var vectors = _db.Search(query, request.K);
            var response = new SearchResponse();
            foreach (var vector in vectors)
            {
                response.Vectors.Add(Utility.ConvertToVectorMessage(vector));
            }
            return Task.FromResult(response);
        }

        public override async Task<Response> AddVector(AddVectorRequest request, ServerCallContext context)
        {
            // Convert the request vector to your application's Vector type
            var vector = Utility.ConvertToVector(request.Vector);

            // Add the vector to the database
            await Task.Run(() => _db.Vectors.Add(vector));

            // Construct the response
            var response = new Response { Success = true};

            return response;
        }

        public override Task<Response> ClearVectors(Request request, ServerCallContext context)
        {
            // Clear the vectors in the database
            _db.Vectors.Clear();

            // Create the response
            var response = new Response { Success = true };

            return Task.FromResult(response);
        }



    }
}
