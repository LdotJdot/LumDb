using System.Text;

namespace LumDbEngine.Utils.StringUtils
{
    internal static class StringExtensions
    {
        public static string TransformToToString(this byte[] bytes)
        {
            if (bytes != null && bytes.Length > 0)
            {
                return Encoding.UTF8.GetString(bytes).Trim('\0');
            }
            else
            {
                return string.Empty;
            }
        }
    }
}