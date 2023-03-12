using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Specialized;
using static HashFunctions;

BlendFile file;
using (var fs = new FileStream("Jinx.blnd", FileMode.Open, FileAccess.Read))
using (var br = new BinaryReader(fs, Encoding.Default))
    file = new BlendFile(br);

//File.WriteAllText("Jinx.blnd.json", JsonConvert.SerializeObject(file, Formatting.Indented));

Memory.Allocate(file);
using (var fs = new FileStream("Jinx.2.blnd", FileMode.Create, FileAccess.Write))
using (var bw = new BinaryWriter(fs, Encoding.Default))
    Memory.Write(bw);

static class Memory
{
    private static MemoryMode Mode = MemoryMode.SizeEstimation;
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
        public Action<BinaryWriter> Write;
        public Record(Action<BinaryWriter> write)
        {
            Write = write;
        }
    }
    public static void Write(BinaryWriter bw, Writable wobj)
    {
        var record = memory.GetValueOrDefault(wobj);
        if(Mode == MemoryMode.SizeEstimation)
        {
            record = new(wobj.Write);
            memory.Add(wobj, record);
            var prevAddr = bw.BaseStream.Position;
            wobj.Write(bw); // ctr(bw)
            record.Size = bw.BaseStream.Position - prevAddr;
        }
        else //if(Mode == MemoryMode.Writing)
        {
            if(record == null)
                throw new Exception("The object must be allocated at the previous stage");
        }
    }
    public static int Allocate(Writable wobj)
    {
        return Allocate(wobj, wobj.Write);
    }
    public static int Allocate(string sobj)
    {
        return Allocate(sobj, bw => bw.Write(Encoding.UTF8.GetBytes(sobj)));
    }
    public static int Allocate(object obj, Action<BinaryWriter> ctr, BinaryWriter? bwo = null)
    {
        var record = memory.GetValueOrDefault(obj);
        if(Mode == MemoryMode.SizeEstimation)
        {
            if(record == null)
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                record = new(ctr);
                memory.Add(obj, record);
                ctr(bw);
                Debug.Assert(ms.Length != 0);
                record.Size = ms.Length;
            }
            return 0;
        }
        else //if(Mode == MemoryMode.Writing)
        {
            if(record == null)
                throw new Exception("The object must be allocated at the previous stage");
        }
        record.Allocate = true;
        //Debug.Assert(record.Addr != -1);
        return (int)record.Addr;
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
    public static void Write(BinaryWriter bw)
    {
        long addr = 0;
        foreach(var record in memory.Values)
        //if(record.Addr == -1)
        if(record.Allocate)
        {
            record.Addr = addr;
            addr += record.Size;
        }
        Mode = MemoryMode.Writing;
        foreach(var record in memory.Values)
            record.Write(bw);
        Mode = MemoryMode.SizeEstimation;
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
        for(int i = 0; i < length; i++)
        {
            var c = br.ReadChar();
            if(c == '\0') break;
            ret += c;
        }
        return ret;
    }

    public static void Write(this BinaryWriter bw, Writable wobj)
    {
        Memory.Write(bw, wobj);
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
    protected void ReadExtraBytes()
    {
        /*
        if(mResourceSize == 0) return;
        long readed = br.BaseStream.Position - 1 - baseAddr;
        ExtraBytes = br.ReadBytes((int)(mResourceSize - readed));
        */
    }
}

class PoolData : Resource
{
    public uint mFormatToken;
    public uint mVersion;
    public bool mUseCascadeBlend;
    public float mCascadeBlendValue;
    
    public BlendData[] mBlendDataAry;
    public TransitionClipData[] mTransitionData;
    public TrackResource[] mBlendTrackAry;
    public ClipResource[] mClassAry;
    public MaskResource[] mMaskDataAry;
    public EventResource[] mEventDataAry;
    public AnimResourceBase[] mAnimDataAry;
    public uint mAnimNameCount;
    public PathRecord[] mAnimNames;
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
        mAnimNameCount = br.ReadUInt32();
        long mAnimNamesOffset = br.ReadAddr();
        mSkeleton = new PathRecord(br);
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
        };
        ReadExtraBytes();

        mBlendDataAry = br.ReadArr(mBlendDataAryAddr, mNumBlends, br => new BlendData(br));
        mTransitionData = br.ReadArr(mTransitionDataOffset, mNumTransitionData, br => new TransitionClipData(br));
        mBlendTrackAry = br.ReadArr(mBlendTrackAryAddr, mNumTracks, br => new TrackResource(br));

        mClassAry = br.ReadArr2(mClassAryAddr, mNumClasses, br => new ClipResource(br));
        mMaskDataAry = br.ReadArr2(mMaskDataAryAddr, mNumMaskData, br => new MaskResource(br));
        mEventDataAry = br.ReadArr2(mEventDataAryAddr, mNumEventData, br => new EventResource(br));
        mAnimDataAry = br.ReadArr2(mAnimDataAryAddr, mNumAnimData, br => new AnimResourceBase(br));
        
        mAnimNames = br.ReadArr(mAnimNamesOffset, mAnimNameCount, br => new PathRecord(br));
    }

    public override void Write(BinaryWriter bw)
    {
        bw.Write(Memory.SizeOf(this));
        bw.Write(mFormatToken);
        bw.Write(mVersion);
        bw.Write(0u); // mNumClasses
        bw.Write(0u); // mNumBlends
        bw.Write(0u); // mNumTransitionData
        bw.Write(0u); // mNumTracks
        bw.Write(0u); // mNumAnimData
        bw.Write(0u); // mNumMaskData
        bw.Write(0u); // mNumEventData
        bw.Write(Convert.ToUInt32(mUseCascadeBlend));
        bw.Write(mCascadeBlendValue);
        bw.Write(0); // mBlendDataAryAddr
        bw.Write(0); // mTransitionDataOffset
        bw.Write(0); // mBlendTrackAryAddr
        bw.Write(0); // mClassAryAddr
        bw.Write(0); // mMaskDataAryAddr
        bw.Write(0); // mEventDataAryAddr
        bw.Write(0); // mAnimDataAryAddr
        bw.Write(0u); // mAnimNameCount
        bw.Write(0); // mAnimNamesOffset
        bw.Write(mSkeleton);
        foreach(uint ui in mExtBuffer)
            bw.Write(ui);
    }
}

class AnimResourceBase : Resource
{
    public uint formatToken;
    public AnimResourceBase(BinaryReader br): base(br)
    {
        formatToken = br.ReadUInt32();
        ReadExtraBytes();
    }
};

class EventResource: Resource
{
    public uint mFormatToken;
    public uint mVersion;
    public ushort mFlags;
    public ushort mNumEvents;
    public uint mUniqueID;
    public BaseEventData[] mEventArray;
    public BaseEventData mEventData;
    public EventNameHash mEventNameHash;
    public class EventNameHash : Writable
    {
        public uint mDataID;
        public uint mNameHash;
        public EventNameHash(BinaryReader br)
        {
            mDataID = br.ReadUInt32();
            mNameHash = br.ReadUInt32();
        }
    }
    public EventFrame mEventFrame;
    public class EventFrame : Writable
    {
        public uint mDataID;
        public float mFrame;
        public EventFrame(BinaryReader br)
        {
            mDataID = br.ReadUInt32();
            mFrame = br.ReadSingle();
        }
    }
    public string mName;
    private uint[] mExtBuffer;
    public EventResource(BinaryReader br): base(br)
    {
        mFormatToken = br.ReadUInt32();
        mVersion = br.ReadUInt32();
        mFlags = br.ReadUInt16();
        mNumEvents = br.ReadUInt16();
        mUniqueID = br.ReadUInt32();
        long mEventOffsetArrayOffset = br.ReadAddr(baseAddr);
        long mEventDataOffset = br.ReadAddr(baseAddr);
        long mEventNameHashOffset = br.ReadAddr(baseAddr);
        long mEventFrameOffset = br.ReadAddr(baseAddr);
        long mNameOffset = br.ReadAddr(baseAddr);
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
            br.ReadUInt32(),
        };
        ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        mEventArray = br.ReadArr2(mEventOffsetArrayOffset, mNumEvents, br => new BaseEventData(br), baseAddr);
        br.BaseStream.Position = mEventDataOffset;
        mEventData = new BaseEventData(br);
        br.BaseStream.Position = mEventNameHashOffset;
        mEventNameHash = new EventNameHash(br);
        br.BaseStream.Position = mEventFrameOffset;
        mEventFrame = new EventFrame(br);
        br.BaseStream.Position = mNameOffset;
        mName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
}

class BaseEventData : Resource
{
    public uint mEventTypeId;
    public uint mFlags;
    public float mFrame;
    public string mName;
    public BaseEventData(BinaryReader br): base(br)
    {
        mEventTypeId = br.ReadUInt32();
        mFlags = br.ReadUInt32();
        mFrame = br.ReadSingle();
        int mNameOffset = br.ReadInt32();
        ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = baseAddr + mNameOffset;
        mName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
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
        path = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        long baseAddr = bw.BaseStream.Position;
        bw.Write(HashStringFNV1a(path));
        bw.Write((int)(Memory.Allocate(path) - baseAddr));
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
        ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mWeightOffset;
        mWeight = br.ReadSingle();
        br.BaseStream.Position = mJointHashOffset;
        mJointHash = new JointHash(br);
        br.BaseStream.Position = mJointNdxOffset;
        mJointNdx = new JointNdx(br);
        br.BaseStream.Position = prevPosition;
    }
};

class ClipResource: Resource
{
    public Flags mFlags;
    public enum Flags
    {
        eNone_0 = 0x0,
        eMain = 0x1,
        eLoop = 0x2,
        eContinue = 0x4,
        ePlayOnce = 0x8,
    }
    public uint mUniqueID;
    public string mName;
    public ClipData mClipData;
    public ClipResource(BinaryReader br): base(br)
    {
        mFlags = (Flags)br.ReadUInt32(); //TODO: Verify (u16)
        mUniqueID = br.ReadUInt32();
        long mNameOffset = br.ReadInt32();
        long mClipDataOffset = br.ReadInt32();
        ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = baseAddr + mNameOffset;
        mName = br.ReadCString();
        br.BaseStream.Position = baseAddr + mClipDataOffset;
        mClipData = new ClipData(br);
        br.BaseStream.Position = prevPosition;
    }
}

class ClipData : Writable
{
    public uint mClipTypeID;
    public ClipData(BinaryReader br)
    {
        mClipTypeID = br.ReadUInt32();
    }
}

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
};

class RelativeArray<T>
{
    public uint mOffsetFromSelf;
}

class Offset<T>
{
    public uint mValue;
}

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
        ReadExtraBytes();
    }
}