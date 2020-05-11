using System.IO;

namespace UnityFS.Utils
{
    public interface IDataChecker
    {
        string hex { get; }
        void Reset();
        void Update(Stream stream);
        void Update(byte[] bytes, int offset, int count);
    }
}