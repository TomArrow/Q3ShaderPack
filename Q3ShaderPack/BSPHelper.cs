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
            public UInt32 surfaceFlags;
            public UInt32 contentFlags;
            public unsafe string getShaderName()
            {
                fixed (byte* shaderPtr = shader)
                {
                    //return Encoding.ASCII.GetString(shaderPtr, MAX_QPATH).TrimEnd((Char)0);
                    return Encoding.ASCII.GetString(shaderPtr, Helpers.getByteCountUntilZero(shaderPtr,MAX_QPATH)).TrimEnd((Char)0);
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

        enum ContentFlag { // order or value doesnt matter, its just to identify it
            CONTENTS_SOLID,
            CONTENTS_LAVA,
            CONTENTS_WATER,
            CONTENTS_FOG,
            CONTENTS_PLAYERCLIP,
            CONTENTS_MONSTERCLIP,
            CONTENTS_BOTCLIP,
            CONTENTS_SHOTCLIP,
            CONTENTS_BODY,
            CONTENTS_CORPSE,
            CONTENTS_TRIGGER,
            CONTENTS_NODROP,
            CONTENTS_TERRAIN,
            CONTENTS_LADDER,
            CONTENTS_ABSEIL,
            CONTENTS_OPAQUE,
            CONTENTS_OUTSIDE,
            CONTENTS_SLIME,
            CONTENTS_LIGHTSABER,
            CONTENTS_TELEPORTER,
            CONTENTS_ITEM,
            CONTENTS_NOSHOT,
            CONTENTS_DETAIL,
            CONTENTS_TRANSLUCENT,
            CONTENTS_AREAPORTAL,
            CONTENTS_JUMPPAD,
            CONTENTS_CLUSTERPORTAL,
            CONTENTS_DONOTENTER,
            CONTENTS_ORIGIN,
            CONTENTS_STRUCTURAL
        }

        enum SurfaceFlag
        { // order or value doesnt matter, its just to identify it
            SURF_SKY,
            SURF_SLICK,
            SURF_METALSTEPS,
            SURF_FORCEFIELD,
            SURF_NODAMAGE,
            SURF_NOIMPACT,
            SURF_NOMARKS,
            SURF_NODRAW,
            SURF_NOSTEPS,
            SURF_NODLIGHT,
            SURF_NOMISCENTS,
            SURF_LADDER,
            SURF_FLESH,
            SURF_HINT,
            SURF_SKIP,
            SURF_NOLIGHTMAP,
            SURF_POINTLIGHT,
            SURF_NONSOLID,
            SURF_LIGHTFILTER,
            SURF_ALPHASHADOW,
        }

        static Dictionary<ContentFlag, UInt32> contentFlagToIntJk2 = new Dictionary<ContentFlag, UInt32>() {
            {ContentFlag.CONTENTS_SOLID,0x00000001},
            {ContentFlag.CONTENTS_LAVA,0x00000002},
            {ContentFlag.CONTENTS_WATER,0x00000004},
            {ContentFlag.CONTENTS_FOG,0x00000008},
            {ContentFlag.CONTENTS_PLAYERCLIP,0x00000010},
            {ContentFlag.CONTENTS_MONSTERCLIP,0x00000020},
            {ContentFlag.CONTENTS_BOTCLIP,0x00000040},
            {ContentFlag.CONTENTS_SHOTCLIP,0x00000080},
            {ContentFlag.CONTENTS_BODY,0x00000100},
            {ContentFlag.CONTENTS_CORPSE,0x00000200},
            {ContentFlag.CONTENTS_TRIGGER,0x00000400},
            {ContentFlag.CONTENTS_NODROP,0x00000800},
            {ContentFlag.CONTENTS_TERRAIN,0x00001000},
            {ContentFlag.CONTENTS_LADDER,0x00002000},
            {ContentFlag.CONTENTS_ABSEIL,0x00004000},
            {ContentFlag.CONTENTS_OPAQUE,0x00008000},
            {ContentFlag.CONTENTS_OUTSIDE,0x00010000},
            {ContentFlag.CONTENTS_SLIME,0x00020000},
            {ContentFlag.CONTENTS_LIGHTSABER,0x00040000},
            {ContentFlag.CONTENTS_TELEPORTER,0x00080000},
            {ContentFlag.CONTENTS_ITEM,0x00100000},
            {ContentFlag.CONTENTS_NOSHOT,0x00200000},
            {ContentFlag.CONTENTS_DETAIL,0x08000000},
            {ContentFlag.CONTENTS_TRANSLUCENT,0x80000000},
        };
        static Dictionary<SurfaceFlag, UInt32> surfaceFlagToIntJk2 = new Dictionary<SurfaceFlag, UInt32>() {
            {SurfaceFlag.SURF_SKY,0x00002000},
            {SurfaceFlag.SURF_SLICK,0x00004000},
            {SurfaceFlag.SURF_METALSTEPS,0x00008000},
            {SurfaceFlag.SURF_FORCEFIELD,0x00010000},
            {SurfaceFlag.SURF_NODAMAGE,0x00040000},
            {SurfaceFlag.SURF_NOIMPACT,0x00080000},
            {SurfaceFlag.SURF_NOMARKS,0x00100000},
            {SurfaceFlag.SURF_NODRAW,0x00200000},
            {SurfaceFlag.SURF_NOSTEPS,0x00400000},
            {SurfaceFlag.SURF_NODLIGHT,0x00800000},
            {SurfaceFlag.SURF_NOMISCENTS,0x01000000},
        };
        static Dictionary<UInt32, ContentFlag> q3IntToContentFlag = new Dictionary<UInt32, ContentFlag>() {
            {1,ContentFlag.CONTENTS_SOLID},
            {8,ContentFlag.CONTENTS_LAVA},
            {16,ContentFlag.CONTENTS_SLIME},
            {32,ContentFlag.CONTENTS_WATER},
            {64,ContentFlag.CONTENTS_FOG},
            {0x8000,ContentFlag.CONTENTS_AREAPORTAL},
            {0x10000,ContentFlag.CONTENTS_PLAYERCLIP},
            {0x20000,ContentFlag.CONTENTS_MONSTERCLIP},
            {0x40000,ContentFlag.CONTENTS_TELEPORTER},
            {0x80000,ContentFlag.CONTENTS_JUMPPAD},
            {0x100000,ContentFlag.CONTENTS_CLUSTERPORTAL},
            {0x200000,ContentFlag.CONTENTS_DONOTENTER},
            {0x1000000,ContentFlag.CONTENTS_ORIGIN},
            {0x2000000,ContentFlag.CONTENTS_BODY},
            {0x4000000,ContentFlag.CONTENTS_CORPSE},
            {0x8000000,ContentFlag.CONTENTS_DETAIL},
            {0x10000000,ContentFlag.CONTENTS_STRUCTURAL},
            {0x20000000,ContentFlag.CONTENTS_TRANSLUCENT},
            {0x40000000,ContentFlag.CONTENTS_TRIGGER},
            {0x80000000,ContentFlag.CONTENTS_NODROP},
        };
        static Dictionary<UInt32, SurfaceFlag> q3IntToSurfaceFlag = new Dictionary<UInt32, SurfaceFlag>() {
            {0x1,SurfaceFlag.SURF_NODAMAGE},
            {0x2,SurfaceFlag.SURF_SLICK},
            {0x4,SurfaceFlag.SURF_SKY},
            {0x8,SurfaceFlag.SURF_LADDER},
            {0x10,SurfaceFlag.SURF_NOIMPACT},
            {0x20,SurfaceFlag.SURF_NOMARKS},
            {0x40,SurfaceFlag.SURF_FLESH},
            {0x80,SurfaceFlag.SURF_NODRAW},
            {0x100,SurfaceFlag.SURF_HINT},
            {0x200,SurfaceFlag.SURF_SKIP},
            {0x400,SurfaceFlag.SURF_NOLIGHTMAP},
            {0x800,SurfaceFlag.SURF_POINTLIGHT},
            {0x1000,SurfaceFlag.SURF_METALSTEPS},
            {0x2000,SurfaceFlag.SURF_NOSTEPS},
            {0x4000,SurfaceFlag.SURF_NONSOLID},
            {0x8000,SurfaceFlag.SURF_LIGHTFILTER},
            {0x10000,SurfaceFlag.SURF_ALPHASHADOW},
            {0x20000,SurfaceFlag.SURF_NODLIGHT},
        };

        static public UInt32 surfaceFlagsQ3ToJK2(UInt32 q3Flags)
        {
            UInt32 retVal = 0;
            UInt32 a = 1;
            for(int i = 0; i < 32; i++)
            {
                UInt32 bit = a << i;
                if((q3Flags & bit) > 0)
                {
                    if (q3IntToSurfaceFlag.ContainsKey(bit))
                    {
                        SurfaceFlag flag = q3IntToSurfaceFlag[bit];
                        if (surfaceFlagToIntJk2.ContainsKey(flag))
                        {
                            retVal |= surfaceFlagToIntJk2[flag];
                        }
                    }
                }
            }
            return retVal;
        }
        static public UInt32 contentFlagsQ3ToJK2(UInt32 q3Flags)
        {
            UInt32 retVal = 0;
            UInt32 a = 1;
            for(int i = 0; i < 32; i++)
            {
                UInt32 bit = a << i;
                if((q3Flags & bit) > 0)
                {
                    if (q3IntToContentFlag.ContainsKey(bit))
                    {
                        ContentFlag flag = q3IntToContentFlag[bit];
                        if (contentFlagToIntJk2.ContainsKey(flag))
                        {
                            retVal |= contentFlagToIntJk2[flag];
                        }
                    }
                }
            }
            return retVal;
        }


        public static void ConvertQ3FlagsToJK2Flags(Stream inStream, Stream outStream)
        {
            inStream.Seek(0, SeekOrigin.Begin);
            outStream.Seek(0, SeekOrigin.Begin);

            inStream.CopyTo(outStream);

            inStream.Seek(0, SeekOrigin.Begin);
            outStream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(inStream))
            {
                using (BinaryWriter bw = new BinaryWriter(outStream,Encoding.Latin1,true))
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
                        bw.BaseStream.Seek(br.BaseStream.Position, SeekOrigin.Begin);
                        dshader_t shaderHere = Helpers.ReadBytesAsType<dshader_t>(br);

                        shaderHere.surfaceFlags = surfaceFlagsQ3ToJK2(shaderHere.surfaceFlags);
                        shaderHere.contentFlags = contentFlagsQ3ToJK2(shaderHere.contentFlags);

                        Helpers.WriteTypeAsBytes(bw,shaderHere);
                    }
                }
            }
        }

        public static unsafe string[] GetShaders(string bspPath,Q3FileSystem fsq3)
        {
            HashSet<string> shaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            //using (FileStream fs = new FileStream(bspPath, FileMode.Open, FileAccess.Read))
            using (Stream fs = fsq3.GetStream(bspPath))
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
