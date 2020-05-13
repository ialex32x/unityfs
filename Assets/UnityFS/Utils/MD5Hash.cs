using System;
using System.IO;

namespace UnityFS.Utils
{
    public class MD5Hash : IDataChecker
    {
        private string _hash;

        public string hex
        {
            get
            {
                return _hash;
            }
        }

        public MD5Hash()
        {
        }
        
        public void ComputeHashFull(Stream stream)
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            _hash = GetString(md5.ComputeHash(stream));
        }

        public void Reset()
        {
        }

        public void Update(Stream stream)
        {
        }
        
        public void Update(byte[] bytes, int offset, int count)
        {
        }
        
        public static string GetString(byte[] bytes)
        {
            var str = "";
            for (int i = 0, len = bytes.Length; i < len; i++)
            {
                str += bytes[i].ToString("x").PadLeft(2, '0');
            }

            return str;
        }
    }
}