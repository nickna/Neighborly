using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Neighborly
{
    public interface IDataPersistence
    {
        static abstract T FromBinaryStream<T>(BinaryReader reader) where T : IDataPersistence;
        public void ToBinaryStream(BinaryWriter writer);
    }
}
