using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using static HashFunctions;

BlendFile ReadBLND(string path)
{
    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (var br = new BinaryReader(fs, Encoding.Default))
    return br.Read<BlendFile>();
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
WriteBLND(f1, "Jinx.2.blnd");
var f2 = ReadBLND("Jinx.2.blnd");
WriteJSON(f2, "Jinx.2.blnd.json");

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

abstract class Writable
{
    public abstract void Write(BinaryWriter bw);
    // {
    //     throw new NotImplementedException();
    // }
}

class BlendFile: Writable
{
    public BinaryHeader Header;
    public PoolData Pool;
    public BlendFile(BinaryReader br)
    {
        Header = br.Read<BinaryHeader>();
        Debug.Assert(
            Header.mEngineType == 845427570u &&
            Header.mBinaryBlockType == 1684958306u &&
            Header.mBinaryBlockVersion == 1u
        );
        Pool = br.Read<PoolData>();
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(Header);
        bw.Write(Pool);
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

abstract class Resource : Writable
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

        mSkeleton = br.Read<PathRecord>();
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
        };
        //ReadExtraBytes();

        if(mBlendDataAryAddr != 0) mBlendDataAry = br.ReadArr(mBlendDataAryAddr, mNumBlends, br => br.Read<BlendData>());
        if(mTransitionDataOffset != 0) mTransitionData = br.ReadArr(mTransitionDataOffset, mNumTransitionData, br => br.Read<TransitionClipData>());
        if(mBlendTrackAryAddr != 0) mBlendTrackAry = br.ReadArr(mBlendTrackAryAddr, mNumTracks, br => br.Read<TrackResource>());

        if(mMaskDataAryAddr != 0) mMaskDataAry = br.ReadArr2(mMaskDataAryAddr, mNumMaskData, br => br.Read<MaskResource>());
        if(mEventDataAryAddr != 0) mEventDataAry = br.ReadArr2(mEventDataAryAddr, mNumEventData, br => br.Read<EventResource>());
        if(mAnimDataAryAddr != 0) mAnimDataAry = br.ReadArr2(mAnimDataAryAddr, mNumAnimData, br => AnimResourceBase.Read(br));
        if(mClassAryAddr != 0) mClassAry = br.ReadArr2(mClassAryAddr, mNumClasses, br => br.Read<ClipResource>());

        if(mAnimNamesOffset != 0) mAnimNames = br.ReadArr(mAnimNamesOffset, mAnimNameCount, br => br.Read<PathRecord>());
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
        if(mJointHashOffset != 0) mJointHash = br.Read<JointHash>();
        br.BaseStream.Position = mJointNdxOffset;
        if(mJointNdxOffset != 0) mJointNdx = br.Read<JointNdx>();
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

        public override void Write(BinaryWriter bw)
        {
            throw new NotImplementedException();
        }
    }
    public TransitionClipData(BinaryReader br)
    {
        mFromAnimId = br.ReadUInt32();
        mTransitionToCount = br.ReadUInt32();

        uint mOffsetFromSelf = br.ReadUInt32();
        mTransitionToArray = new TransitionToData[mTransitionToCount];
        for(int i = 0; i < mTransitionToCount; i++)
            mTransitionToArray[i] = br.Read<TransitionToData>();
    }

    public override void Write(BinaryWriter bw)
    {
        throw new NotImplementedException();
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