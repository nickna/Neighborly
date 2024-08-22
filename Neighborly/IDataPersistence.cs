using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    public interface IDataPersistence
    {
        // use a BinaryReader to import all object data from a binary stream
        // <example>
        // public yourClass(BinaryReader reader)

        /// <summary>
        /// Export all object data to a binary stream
        /// </summary>
        /// <param name="writer"></param>
        public void ToBinaryStream(BinaryWriter writer);

        public byte[] ToBinary()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    ToBinaryStream(writer);
                }
                return memoryStream.ToArray();
            }
        }
    }
}
