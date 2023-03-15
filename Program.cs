using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using static HashFunctions;

BlendFile ReadBLND(string path)
{
    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (var br = new BinaryReader(fs, Encoding.Default))
        return new BlendFile(br);
}
void WriteJSON(BlendFile file, string path)
{
    File.WriteAllText(path, JsonConvert.SerializeObject(file, Formatting.Indented));
}
void WriteBLND(BlendFile file, string path)
{
    File.WriteAllBytes(path, Memory.Process(bw => Memory.Allocate(0, file)));
}

var f1 = ReadBLND("Jinx.blnd");
//f1.Pool.mBlendDataAry = null;
//f1.Pool.mTransitionData = null;
//f1.Pool.mBlendTrackAry = null;
//f1.Pool.mClassAry = null;
//f1.Pool.mMaskDataAry = null;
//f1.Pool.mEventDataAry = null;
//f1.Pool.mAnimDataAry = null;
//f1.Pool.mAnimNames = null;

WriteJSON(f1, "Jinx.blnd.json");
//WriteBLND(f1, "Jinx.2.blnd");
//var f2 = ReadBLND("Jinx.2.blnd");
//WriteJSON(f2, "Jinx.2.blnd.json");

static class Memory
{
    private static MemoryMode Mode;
    private enum MemoryMode
    {
        SizeEstimation,
        Writing
    }
    private static Dictionary<object, Record> memory = new();
    private class Record
    {
        public long Addr = -1, Size = -1;
        public bool Allocate = false;
    }
    public static void Write(BinaryWriter bw, Writable? wobj)
    {
        if(wobj == null) return;

        var record = memory.GetValueOrDefault(wobj);
        if(Mode == MemoryMode.SizeEstimation)
        {
            if(record == null)
            {
                record = new();
                memory.Add(wobj, record);
                var prevPosition = bw.BaseStream.Position;
                wobj.Write(bw);
                record.Size = bw.BaseStream.Position - prevPosition;
            }
            else
                wobj.Write(bw);
        }
        else //if(Mode == MemoryMode.Writing)
        {
            if(record == null)
                throw new Exception("The object must be allocated at the previous stage");
            wobj.Write(bw);
        }
    }
    public static int Allocate(int offset, Writable? wobj)
    {
        if(wobj == null) return 0;
        return Allocate(offset, wobj, wobj.Write);
    }
    public static int Allocate(int offset, string? sobj)
    {
        if(sobj == null) return 0;
        return Allocate(offset, sobj, bw => bw.WriteCString(sobj));
    }
    public static int AllocateArray(int offset, Writable?[]? array)
    {
        if(array == null) return 0;
        return Allocate(offset, array, bw =>
        {
            foreach(var item in array)
                bw.Write(item);
        });
    }
    public static int AllocateArray2(int offset, Writable?[]? array, int? baseAddr = null)
    {
        if(array == null) return 0;
        return Allocate(offset, array, bw =>
        {
            int addr = baseAddr ?? (int)bw.BaseStream.Position;
            bw.Write
            (
                array.Select(item => Allocate(addr, item))
                     .SelectMany(BitConverter.GetBytes).ToArray()
            );
        });
    }
    public static int Allocate(int offset, object? obj, Action<BinaryWriter> ctr)
    {
        if(obj == null) return 0;
        var record = memory.GetValueOrDefault(obj);
        if(Mode == MemoryMode.SizeEstimation)
        {
            if(record == null)
            {
                record = new();
                memory.Add(obj, record);
                var prevPosition = bw.BaseStream.Position;
                ctr(bw);
                record.Size = bw.BaseStream.Position - prevPosition;
                //Debug.Assert(record.Size != 0);
                bw.BaseStream.Position = prevPosition;
            }
            record.Allocate = true;
            return 0;
        }
        else //if(Mode == MemoryMode.Writing)
        {
            if(record == null)
                throw new Exception("The object must be allocated at the previous stage");
            if(record.Allocate)
            {
                record.Allocate = false;
                var prevPosition = bw.BaseStream.Position;
                bw.BaseStream.Position = record.Addr;
                ctr(bw);
                bw.BaseStream.Position = prevPosition;
            }
            //Debug.Assert(record.Addr != -1);
            return (int)record.Addr - offset;
        }
    }
    public static uint SizeOf(object obj)
    {
        if(Mode == MemoryMode.SizeEstimation)
            return 0;
        else
        {
            var record = memory.GetValueOrDefault(obj);
            if(record == null)
                throw new Exception("The object must be allocated at the previous stage");
            return (uint)record.Size;
        }
    }
    static BinaryWriter bw; //HACK:
    public static byte[] Process(Action<BinaryWriter> ctr)
    {
        
        using (var ms = new MemoryStream())
        using (bw = new BinaryWriter(ms))
        {
            Mode = MemoryMode.SizeEstimation;
            ctr(bw);
            long addr = 0;
            foreach(var record in memory.Values)
            if(record.Allocate)
            {
                record.Addr = addr;
                addr += record.Size;
            }
            Mode = MemoryMode.Writing;
            bw.BaseStream.Position = 0;
            ctr(bw);
            return ms.ToArray();
        }
    }
}

static class HashFunctions
{
    public static uint HashStringFNV1a(string s, uint a2 = 0x811C9DC5)
    {
        uint result = a2;
        for (int i = 0; i < s.Length; i++)
            result = 16777619 * (result ^ s[i]);
        return result;
    }
}

class Writable
{
    public virtual void Write(BinaryWriter bw)
    {
        throw new NotImplementedException();
    }
}

class BlendFile: Writable
{
    public BinaryHeader Header;
    public PoolData Pool;
    public BlendFile(BinaryReader br)
    {
        Header = new BinaryHeader(br);
        Debug.Assert(
            Header.mEngineType == 845427570u &&
            Header.mBinaryBlockType == 1684958306u &&
            Header.mBinaryBlockVersion == 1u
        );
        Pool = new PoolData(br);
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(Header);
        bw.Write(Pool);
    }
}

static class BinaryReaderExtensions
{
    public static long ReadAddr(this BinaryReader br)
    {
        long pos = br.BaseStream.Position;
        return br.ReadAddr(pos); 
    }

    public static long ReadAddr(this BinaryReader br, long pos)
    {
        int addr = br.ReadInt32();
        return (addr != 0) ? pos + addr : 0; 
    }

    public static T[] ReadArr<T>(this BinaryReader br, long addr, uint num, Func<BinaryReader, T> ctr)
    {
        var arr = new T[num];
        br.BaseStream.Position = addr;
        for(int i = 0; i < num; i++)
            arr[i] = ctr(br);
        return arr;
    }

    public static T[] ReadArr2<T>(this BinaryReader br, long addr, uint num, Func<BinaryReader, T> ctr, long? baseAddr = null)
    {
        //var prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = addr;

        var addrs = new long[num];
        for(int i = 0; i < num; i++)
            addrs[i] = br.ReadAddr(baseAddr ?? addr);
        
        var arr = new T[num];
        for(int i = 0; i < num; i++)
        {
            addr = addrs[i];
            if(addr != 0) //TODO: throw an Exception otherwise?
            {
                br.BaseStream.Position = addr;
                arr[i] = ctr(br);
            }
        }

        //br.BaseStream.Position = prevPosition;
        return arr;
    }

    public static string ReadCString(this BinaryReader br, int length = int.MaxValue)
    {
        var ret = "";
        var prevPosition = br.BaseStream.Position;
        for(int i = 0; i < length; i++)
        {
            var c = br.ReadChar();
            if(c == '\0') break;
            ret += c;
        }
        if(length != int.MaxValue)
            br.BaseStream.Position = prevPosition + length;
        return ret;
    }

    public static void Write(this BinaryWriter bw, Writable? wobj)
    {
        Memory.Write(bw, wobj);
    }

    public static void WriteCString(this BinaryWriter bw, string sobj)
    {
        bw.Write(Encoding.UTF8.GetBytes(sobj + '\0'));
    }

    public static void WriteCString(this BinaryWriter bw, string sobj, int length)
    {
        length--;
        sobj = sobj.PadRight(length, '\0')[..length];
        var bytes = Encoding.UTF8.GetBytes(sobj + '\0');
        bw.Write(bytes);
    }
}

class BinaryHeader : Writable
{
    public uint mEngineType;
    public uint mBinaryBlockType;
    public uint mBinaryBlockVersion;
    public BinaryHeader(BinaryReader br)
    {
        mEngineType = br.ReadUInt32();
        mBinaryBlockType = br.ReadUInt32();
        mBinaryBlockVersion = br.ReadUInt32();
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(mEngineType);
        bw.Write(mBinaryBlockType);
        bw.Write(mBinaryBlockVersion);
    }
}

class Resource : Writable
{
    protected long baseAddr;
    public uint mResourceSize;
    //public byte[] ExtraBytes;
    private BinaryReader br;
    public Resource(BinaryReader br)
    {
        this.br = br;
        baseAddr = br.BaseStream.Position;
        mResourceSize = br.ReadUInt32();
    }
    /*
    protected void ReadExtraBytes()
    {
        if(mResourceSize == 0) return;
        long readed = br.BaseStream.Position - 1 - baseAddr;
        ExtraBytes = br.ReadBytes((int)(mResourceSize - readed));
    }
    */
}

class PoolData : Resource
{
    public uint mFormatToken;
    public uint mVersion;
    public bool mUseCascadeBlend;
    public float mCascadeBlendValue;
    
    public BlendData[]? mBlendDataAry;
    public TransitionClipData[]? mTransitionData;
    public TrackResource[]? mBlendTrackAry;
    public ClipResource[]? mClassAry;
    public MaskResource[]? mMaskDataAry;
    public EventResource[]? mEventDataAry;
    public AnimResourceBase?[]? mAnimDataAry;
    public PathRecord[]? mAnimNames;
    public PathRecord mSkeleton;

    private uint[] mExtBuffer;

    public PoolData(BinaryReader br): base(br)
    {
        mFormatToken = br.ReadUInt32();
        mVersion = br.ReadUInt32();
        uint mNumClasses = br.ReadUInt32();
        uint mNumBlends = br.ReadUInt32();
        uint mNumTransitionData = br.ReadUInt32();
        uint mNumTracks = br.ReadUInt32();
        uint mNumAnimData = br.ReadUInt32();
        uint mNumMaskData = br.ReadUInt32();
        uint mNumEventData = br.ReadUInt32();
        mUseCascadeBlend = br.ReadUInt32() != 0; //TODO: Verify
        mCascadeBlendValue = br.ReadSingle();

        long mBlendDataAryAddr = br.ReadAddr();
        long mTransitionDataOffset = br.ReadAddr();
        long mBlendTrackAryAddr = br.ReadAddr();
        long mClassAryAddr = br.ReadAddr();
        long mMaskDataAryAddr = br.ReadAddr();
        long mEventDataAryAddr = br.ReadAddr();
        long mAnimDataAryAddr = br.ReadAddr();
        uint mAnimNameCount = br.ReadUInt32();
        long mAnimNamesOffset = br.ReadAddr();

        mSkeleton = new PathRecord(br);
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
        };
        //ReadExtraBytes();

        if(mBlendDataAryAddr != 0) mBlendDataAry = br.ReadArr(mBlendDataAryAddr, mNumBlends, br => new BlendData(br));
        if(mTransitionDataOffset != 0) mTransitionData = br.ReadArr(mTransitionDataOffset, mNumTransitionData, br => new TransitionClipData(br));
        if(mBlendTrackAryAddr != 0) mBlendTrackAry = br.ReadArr(mBlendTrackAryAddr, mNumTracks, br => new TrackResource(br));

        if(mMaskDataAryAddr != 0) mMaskDataAry = br.ReadArr2(mMaskDataAryAddr, mNumMaskData, br => new MaskResource(br));
        if(mEventDataAryAddr != 0) mEventDataAry = br.ReadArr2(mEventDataAryAddr, mNumEventData, br => new EventResource(br));
        if(mAnimDataAryAddr != 0) mAnimDataAry = br.ReadArr2(mAnimDataAryAddr, mNumAnimData, br => AnimResourceBase.Read(br));
        if(mClassAryAddr != 0) mClassAry = br.ReadArr2(mClassAryAddr, mNumClasses, br => new ClipResource(br, this));

        if(mAnimNamesOffset != 0) mAnimNames = br.ReadArr(mAnimNamesOffset, mAnimNameCount, br => new PathRecord(br));
    }

    public override void Write(BinaryWriter bw)
    {
        int baseAddr = (int)bw.BaseStream.Position;

        bw.Write(Memory.SizeOf(this));
        bw.Write(mFormatToken);
        bw.Write(mVersion);
        bw.Write(mClassAry?.Length ?? 0);
        bw.Write(mBlendDataAry?.Length ?? 0);
        bw.Write(mTransitionData?.Length ?? 0);
        bw.Write(mBlendTrackAry?.Length ?? 0);
        bw.Write(mAnimDataAry?.Length ?? 0);
        bw.Write(mMaskDataAry?.Length ?? 0);
        bw.Write(mEventDataAry?.Length ?? 0);
        bw.Write(Convert.ToUInt32(mUseCascadeBlend));
        bw.Write(mCascadeBlendValue);

        int mBlendDataAryAddr;
        int mTransitionDataOffset;
        int mBlendTrackAryAddr;
        int mClassAryAddr;
        int mMaskDataAryAddr;
        int mEventDataAryAddr;
        int mAnimDataAryAddr;
        int mAnimNameCount;
        int mAnimNamesOffset;

        int c() => (int)bw.BaseStream.Position;
        bw.Write(mBlendDataAryAddr = Memory.AllocateArray(c(), mBlendDataAry));
        bw.Write(mTransitionDataOffset = Memory.AllocateArray(c(), mTransitionData));
        bw.Write(mBlendTrackAryAddr = Memory.AllocateArray(c(), mBlendTrackAry));

        bw.Write(mClassAryAddr = Memory.AllocateArray2(c(), mClassAry));
        bw.Write(mMaskDataAryAddr = Memory.AllocateArray2(c(), mMaskDataAry));
        bw.Write(mEventDataAryAddr = Memory.AllocateArray2(c(), mEventDataAry));
        bw.Write(mAnimDataAryAddr = Memory.AllocateArray2(c(), mAnimDataAry));

        bw.Write(mAnimNameCount = mAnimNames?.Length ?? 0);
        bw.Write(mAnimNamesOffset = Memory.AllocateArray(c(), mAnimNames));
        
        bw.Write(mSkeleton);
        
        foreach(uint ui in mExtBuffer)
            bw.Write(ui);
    }
}

class PathRecord : Writable
{
    public string path;
    public PathRecord(BinaryReader br)
    {
        uint pathHash = br.ReadUInt32();
        long pathOffset = br.ReadAddr(); //TODO: Verify

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = pathOffset;
        if(pathOffset != 0) path = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        int c() => (int)bw.BaseStream.Position;
        bw.Write(HashStringFNV1a(path));
        bw.Write(Memory.Allocate(c(), path));
    }
}

class MaskResource : Resource
{
    public uint mFormatToken;
    public uint mVersion;
    public ushort mFlags;
    public ushort mNumElement;
    public uint mUniqueID;
    public float mWeight;
    public JointHash mJointHash;
    public class JointHash : Writable
    {
        public int mWeightID;
        public uint mJointHash;
        public JointHash(BinaryReader br)
        {
            mWeightID = br.ReadInt32();
            mJointHash = br.ReadUInt32();
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(mWeightID);
            bw.Write(mJointHash);
        }
    }
    public JointNdx mJointNdx;
    public class JointNdx : Writable
    {
        public int mWeightID;
        public int mJointNdx;
        public JointNdx(BinaryReader br)
        {
            mWeightID = br.ReadInt32();
            mJointNdx = br.ReadInt32();
        }
        public override void Write(BinaryWriter bw)
        {
            bw.Write(mWeightID);
            bw.Write(mJointNdx);
        }
    }
    public string mName;
    private uint[] mExtBuffer;
    public MaskResource(BinaryReader br): base(br)
    {
        mFormatToken = br.ReadUInt32();
        mVersion = br.ReadUInt32();
        mFlags = br.ReadUInt16();
        mNumElement = br.ReadUInt16();
        mUniqueID = br.ReadUInt32();
        long mWeightOffset = br.ReadAddr(baseAddr);
        long mJointHashOffset = br.ReadAddr(baseAddr);
        long mJointNdxOffset = br.ReadAddr(baseAddr);
        mName = br.ReadCString(32);
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
            br.ReadUInt32(),
        };
        //ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mWeightOffset;
        if(mWeightOffset != 0) mWeight = br.ReadSingle();
        br.BaseStream.Position = mJointHashOffset;
        if(mJointHashOffset != 0) mJointHash = new JointHash(br);
        br.BaseStream.Position = mJointNdxOffset;
        if(mJointNdxOffset != 0) mJointNdx = new JointNdx(br);
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        int baseAddr = (int)bw.BaseStream.Position;
        bw.Write(Memory.SizeOf(this));
        bw.Write(mFormatToken);
        bw.Write(mVersion);
        bw.Write(mFlags);
        bw.Write(mNumElement);
        bw.Write(mUniqueID);
        bw.Write(Memory.Allocate(baseAddr, mWeight, bw => bw.Write(mWeight)));
        bw.Write(Memory.Allocate(baseAddr, mJointHash));
        bw.Write(Memory.Allocate(baseAddr, mJointNdx));
        bw.WriteCString(mName, 32);
        foreach(uint ui in mExtBuffer)
            bw.Write(ui);
    }
};

class BlendData : Writable
{
    public uint mFromAnimId;
    public uint mToAnimId;
    public uint mBlendFlags;
    public float mBlendTime;
    public BlendData(BinaryReader br)
    {
        mFromAnimId = br.ReadUInt32();
        mToAnimId = br.ReadUInt32();
        mBlendFlags = br.ReadUInt32();
        mBlendTime = br.ReadSingle();
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(mFromAnimId);
        bw.Write(mToAnimId);
        bw.Write(mBlendFlags);
        bw.Write(mBlendTime);
    }
};

class TransitionClipData : Writable
{
    public uint mFromAnimId;
    public uint mTransitionToCount;
    public TransitionToData[] mTransitionToArray;
    public class TransitionToData : Writable
    {
        public uint mToAnimId;
        public uint mTransitionAnimId;
        public TransitionToData(BinaryReader br)
        {
            mToAnimId = br.ReadUInt32();
            mTransitionAnimId = br.ReadUInt32();
        }
    }
    public TransitionClipData(BinaryReader br)
    {
        mFromAnimId = br.ReadUInt32();
        mTransitionToCount = br.ReadUInt32();

        uint mOffsetFromSelf = br.ReadUInt32();
        mTransitionToArray = new TransitionToData[mTransitionToCount];
        for(int i = 0; i < mTransitionToCount; i++)
            mTransitionToArray[i] = new TransitionToData(br);
    }
}

class TrackResource: Resource
{
    public float mBlendWeight;
    public uint mBlendMode;
    public uint mIndex;
    public string mName;
    public TrackResource(BinaryReader br): base(br)
    {
        mBlendWeight = br.ReadSingle();
        mBlendMode = br.ReadUInt32();
        mIndex = br.ReadUInt32();
        mName = br.ReadCString(32);
        //ReadExtraBytes();
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(Memory.SizeOf(this));
        bw.Write(mBlendWeight);
        bw.Write(mBlendMode);
        bw.Write(mIndex);
        bw.WriteCString(mName, 32);
    }
}