using Neighborly;
using Neighborly.API.Protos;
namespace Neighborly.API
{
    public static class Utility
    {
        public static Neighborly.Vector ConvertToVector(VectorMessage vectorMessage)
        {
            // Convert the ByteString to a byte array
            byte[] values = vectorMessage.Values.ToByteArray();

            // Create a new Vector with the byte array
            Neighborly.Vector vector = Neighborly.Vector.FromBinary(values);

            return vector;
        }

        public static VectorMessage ConvertToVectorMessage(Vector vector)
        {
            // Convert the byte array to a ByteString
            Google.Protobuf.ByteString values = Google.Protobuf.ByteString.CopyFrom(vector.GetBinaryValues());

            // Create a new VectorMessage with the ByteString
            VectorMessage vectorMessage = new VectorMessage { Values = values };

            return vectorMessage;
        }
    }
}
