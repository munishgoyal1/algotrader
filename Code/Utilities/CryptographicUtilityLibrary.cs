using System.Security.Cryptography;
using System.Text;

namespace DMTools.Utilities
{
    /// <summary>
    /// Utility Library for all cryptographic needs of the product suite
    /// </summary>
    public static class CryptographicUtilityLibrary
    {
        /// <summary>
        /// Hash an input string and return the hash as a 32 character hexadecimal string.
        /// </summary>
        /// <param name="input">Input string to hash</param>
        /// <returns>32 character hash of the input string</returns>
        public static string getMd5Hash(string input)
        {
            if (input == null)
                return string.Empty;

            MD5 md5Hasher = MD5.Create();
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            //Cleanup
            md5Hasher = null;
            data = null;

            return sBuilder.ToString();
        }
    }
}
