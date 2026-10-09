using System;
using System.IO;
using System.Text;

namespace Learnest.Core
{
    /// <summary>UTF-8 helpers for native interop and file IO.</summary>
    public static class Utf8
    {
        public static readonly UTF8Encoding Encoding = new UTF8Encoding(false, false);

        /// <summary>Allocate an unmanaged, null terminated UTF-8 copy of <paramref name="value"/>.</summary>
        public static IntPtr Alloc(string value)
        {
            if (value == null) { value = string.Empty; }
            int count = Encoding.GetByteCount(value);
            IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(count + 1);
            System.Runtime.InteropServices.Marshal.Copy(Encoding.GetBytes(value), 0, buffer, count);
            System.Runtime.InteropServices.Marshal.WriteByte(buffer, count, 0);
            return buffer;
        }

        public static void Free(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero) { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
        }

        /// <summary>Read a null terminated UTF-8 string from native memory.</summary>
        public static string Read(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) { return null; }
            int length = 0;
            while (System.Runtime.InteropServices.Marshal.ReadByte(pointer, length) != 0) { length++; }
            if (length == 0) { return string.Empty; }
            byte[] bytes = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.GetString(bytes);
        }

        /// <summary>Read <paramref name="count"/> bytes of UTF-8 from native memory.</summary>
        public static string Read(IntPtr pointer, int count)
        {
            if (pointer == IntPtr.Zero || count <= 0) { return string.Empty; }
            byte[] bytes = new byte[count];
            System.Runtime.InteropServices.Marshal.Copy(pointer, bytes, 0, count);
            return Encoding.GetString(bytes);
        }

        public static void WriteAllText(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
            File.WriteAllText(path, content, Encoding);
        }

        public static string ReadAllText(string path)
        {
            return File.ReadAllText(path, Encoding);
        }
    }
}
