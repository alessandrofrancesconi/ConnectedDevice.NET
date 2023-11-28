using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectedDevice.NET
{
    public class Utils
    {
        public static int IndexOfBytes(byte[] haystack, byte[] needle)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = 0; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }
    }
}
