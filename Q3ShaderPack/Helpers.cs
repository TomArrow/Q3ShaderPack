using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Q3ShaderPack
{
    static class Helpers
    {
        public static T ReadBytesAsType<T>(BinaryReader br, long byteOffset = -1, SeekOrigin seekOrigin = SeekOrigin.Begin)
        {
            if (!(byteOffset == -1 && seekOrigin == SeekOrigin.Begin))
            {
                br.BaseStream.Seek(byteOffset, seekOrigin);
            }
            byte[] bytes = br.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T retVal = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return retVal;
        }
        public static void WriteTypeAsBytes<T>(BinaryWriter bw, T thing)
        {
            byte[] bytes = new byte[Marshal.SizeOf(typeof(T))];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Marshal.StructureToPtr(thing, handle.AddrOfPinnedObject(),false);
            bw.Write(bytes);
            handle.Free();
        }
        public static T ArrayBytesAsType<T, B>(B data, int byteOffset = 0)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T retVal = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject() + byteOffset, typeof(T));
            handle.Free();
            return retVal;
        }

        public static byte[] cutoffByteArrayAtZero(byte[] input)
        {
            for(int i = 0; i < input.Length; i++)
            {
                if(input[i] == 0)
                {
                    byte[] newArr = new byte[i];
                    Buffer.BlockCopy(input, 0, newArr, 0, i);
                    return newArr;
                }
            }
            return input;
        }
        public static unsafe int getByteCountUntilZero(byte* input, int max)
        {
            for(int i = 0; i < max; i++)
            {
                if(input[i] == 0)
                {
                    return i;
                }
            }
            return max;
        }
    }
}
