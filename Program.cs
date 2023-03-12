using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

BlendFile file;
using (FileStream fs = new FileStream("Jinx.blnd", FileMode.Open))
using (BinaryReader br = new BinaryReader(fs, Encoding.Default))
    file = new BlendFile(br);

File.WriteAllText("Jinx.blnd.json", JsonConvert.SerializeObject(file, Formatting.Indented));

class BlendFile
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
}

class BinaryHeader
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
}

class Resource
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
    public uint mNumClasses;
    public uint mNumBlends;
    public uint mNumTransitionData;
    public uint mNumTracks;
    public uint mNumAnimData;
    public uint mNumMaskData;
    public uint mNumEventData;
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
        mNumClasses = br.ReadUInt32();
        mNumBlends = br.ReadUInt32();
        mNumTransitionData = br.ReadUInt32();
        mNumTracks = br.ReadUInt32();
        mNumAnimData = br.ReadUInt32();
        mNumMaskData = br.ReadUInt32();
        mNumEventData = br.ReadUInt32();
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
    public class EventNameHash
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
    public class EventFrame
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

class PathRecord
{
    public uint pathHash;
    public string path;
    public PathRecord(BinaryReader br)
    {
        pathHash = br.ReadUInt32();
        long pathOffset = br.ReadAddr(); //TODO: Verify

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = pathOffset;
        path = br.ReadCString();
        br.BaseStream.Position = prevPosition;
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
    public class JointHash
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
    public class JointNdx
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

class ClipData
{
    public uint mClipTypeID;
    public ClipData(BinaryReader br)
    {
        mClipTypeID = br.ReadUInt32();
    }
}

class BlendData
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

class TransitionClipData
{
    public uint mFromAnimId;
    public uint mTransitionToCount;
    public TransitionToData[] mTransitionToArray;
    public class TransitionToData
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