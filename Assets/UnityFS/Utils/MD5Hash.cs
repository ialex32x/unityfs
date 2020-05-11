using System;
using System.IO;

namespace UnityFS.Utils
{
    public class MD5Hash : IDataChecker
    {
        private System.Security.Cryptography.MD5 _md5;

        public string hex
        {
            get
            {
                return GetString(_md5.Hash);
            }
        }

        public MD5Hash()
        {
            _md5 = System.Security.Cryptography.MD5.Create();
        }
        
        public void Reset()
        {
            _md5.Initialize();
        }

        public void Update(Stream stream)
        {
            _md5.ComputeHash(stream);
        }
        
        public void Update(byte[] bytes, int offset, int count)
        {
            _md5.ComputeHash(bytes, offset, count);
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