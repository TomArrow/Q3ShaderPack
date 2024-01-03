using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Q3ShaderPack
{
    static class BSPHelper
    {
        const int MAX_QPATH = 64;		// max length of a quake game pathname

        const int LUMP_ENTITIES = 0;
        const int LUMP_SHADERS = 1;
        const int LUMP_PLANES = 2;
        const int LUMP_NODES = 3;
        const int LUMP_LEAFS = 4;   
        const int LUMP_LEAFSURFACES = 5;
        const int LUMP_LEAFBRUSHES = 6;
        const int LUMP_MODELS = 7;
        const int LUMP_BRUSHES = 8;
        const int LUMP_BRUSHSIDES = 9;
        const int LUMP_DRAWVERTS = 10;
        const int LUMP_DRAWINDEXES = 11;
        const int LUMP_FOGS = 12;
        const int LUMP_SURFACES = 13;
        const int LUMP_LIGHTMAPS = 14;
        const int LUMP_LIGHTGRID = 15;
        const int LUMP_VISIBILITY = 16;
        const int LUMP_LIGHTARRAY = 17;
        const int HEADER_LUMPS = 18;

        const int MAXLIGHTMAPS = 4;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct lump_t
        {
            public int fileofs, filelen;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct dheader_t
        {
            public int ident;
            public int version;

            public unsafe fixed int lumps[HEADER_LUMPS * 2]; // lump_t is really just 2 ints: fileofs, filelen

            public lump_t GetLump(int index)
            {
                return Helpers.ArrayBytesAsType<lump_t, dheader_t>(this, (int)Marshal.OffsetOf<dheader_t>("lumps") + Marshal.SizeOf(typeof(lump_t)) * index);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct dshader_t
        {
            public unsafe fixed byte shader[MAX_QPATH];
            public int surfaceFlags;
            public int contentFlags;
            public unsafe string getShaderName()
            {
                fixed (byte* shaderPtr = shader)
                {
                    return Encoding.ASCII.GetString(shaderPtr, MAX_QPATH).TrimEnd((Char)0);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct mapVert_t
        {
            public unsafe fixed float xyz[3];
            public unsafe fixed float st[2];
            public unsafe fixed float lightmap[MAXLIGHTMAPS * 2];
            public unsafe fixed float normal[3];
            public unsafe fixed byte color[MAXLIGHTMAPS * 4];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct dsurface_t
        {
            public int shaderNum;
            public int fogNum;
            public int surfaceType;

            public int firstVert;
            public int numVerts;

            public int firstIndex;
            public int numIndexes;

            public unsafe fixed byte lightmapStyles[MAXLIGHTMAPS], vertexStyles[MAXLIGHTMAPS];
            public unsafe fixed int lightmapNum[MAXLIGHTMAPS];
            public unsafe fixed int lightmapX[MAXLIGHTMAPS], lightmapY[MAXLIGHTMAPS];
            public int lightmapWidth, lightmapHeight;

            public unsafe fixed float lightmapOrigin[3];
            public unsafe fixed float lightmapVecs[9]; // for patches, [0] and [1] are lodbounds

            public int patchWidth;
            public int patchHeight;
        }

        struct EzAccessTriangle
        {
            public Vector3[] points;
            public float minX, maxX, minY, maxY;
        }


        enum ShaderType
        {
            NORMAL,
            SYSTEM,
            SKY,
        }


        public static unsafe string[] GetShaders(string bspPath)
        {
            HashSet<string> shaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            using (FileStream fs = new FileStream(bspPath, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    dheader_t header = Helpers.ReadBytesAsType<dheader_t>(br);
                    if (header.version != 1)
                    {
                        throw new Exception("BSP header version is not 1");
                    }

                    lump_t shadersLump = header.GetLump(LUMP_SHADERS);

                    int shaderCount = shadersLump.filelen / Marshal.SizeOf(typeof(dshader_t));

                    // Make look up table that quickly tells us what kind of shader a particular shader index is. Is it a system shader or sky shader? So we quickly see whether a surface should be considered as "walkable"
                    ShaderType[] shaderTypeLUT = new ShaderType[shaderCount];
                    br.BaseStream.Seek(shadersLump.fileofs, SeekOrigin.Begin);
                    for (int i = 0; i < shaderCount; i++)
                    {
                        dshader_t shaderHere = Helpers.ReadBytesAsType<dshader_t>(br);
                        string shaderName = shaderHere.getShaderName();
                        shaders.Add(shaderName);
                    }
                }
            }
            return shaders.ToArray();
        }


    }

}
