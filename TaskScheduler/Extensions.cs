using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Extensions
{
    public static class Extensions
    {
        public static bool CaseInsensitiveContains(this string text, string value,
            StringComparison stringComparison = StringComparison.CurrentCultureIgnoreCase)
        {
            if (text == null & value == null) return true;
            else if (text == null || value == null) return false;
            else return text.IndexOf(value, stringComparison) >= 0;
        }
        public static string CheckFolderPath(this string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return folderPath;
            string rightMostChar = folderPath.Substring(folderPath.Length - 1);
            if (rightMostChar != Path.DirectorySeparatorChar.ToString())
            {
                folderPath += Path.DirectorySeparatorChar;
            }
            if(System.IO.Directory.Exists(folderPath))
            {
                return folderPath;
            }
            else
            {
                System.IO.Directory.CreateDirectory(folderPath);
                return folderPath;
            }
        }
        public static bool IsNumericType(this Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
        public static SecureString ToSecureString(this string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;
            else
            {
                SecureString result = new SecureString();
                foreach (char c in source.ToCharArray())
                    result.AppendChar(c);
                return result;
            }
        }
        public static DateTime? GetFirstDateFromString(string inputText, string regexStr, string dateTimeFormat)
        {

            var regex = new Regex(regexStr);
            foreach (Match m in regex.Matches(inputText))
            {
                if (DateTime.TryParseExact(m.Value, dateTimeFormat, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dt))
                    return dt;
            }
            return null;
        }
    }
}
