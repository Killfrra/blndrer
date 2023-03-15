
using System.Diagnostics;

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
    public ClipResource(BinaryReader br, PoolData pool): base(br)
    {
        mFlags = (Flags)br.ReadUInt32(); //TODO: Verify (u16)
        mUniqueID = br.ReadUInt32();
        long mNameOffset = br.ReadAddr(baseAddr);
        long mClipDataOffset = br.ReadAddr(baseAddr);
        //ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mNameOffset;
        if(mNameOffset != 0) mName = br.ReadCString();
        br.BaseStream.Position = mClipDataOffset;
        if(mClipDataOffset != 0) mClipData = ClipData.Read(br, pool);
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        int baseAddr = (int)bw.BaseStream.Position;
        bw.Write(Memory.SizeOf(this));
        bw.Write((uint)mFlags);
        bw.Write(mUniqueID);
        bw.Write(Memory.Allocate(baseAddr, mName));
        bw.Write(Memory.Allocate(baseAddr, mClipData));
    }
}

class ClipData : Writable
{
    public ClipTypes mClipTypeID;
    public enum ClipTypes
    {
        eInvalid = 0x0,
        eAtomic = 0x1,
        eSelector = 0x2,
        eSequencer = 0x3,
        eParallel = 0x4,
        eMultiChildClip = 0x5,
        eParametric = 0x6,
        eConditionBool = 0x7,
        eConditionFloat = 0x8,
    };
    protected long baseAddr;
    protected ClipData(BinaryReader br)
    {
        baseAddr = br.BaseStream.Position;
        mClipTypeID = (ClipTypes)br.ReadUInt32();
    }
    public static ClipData Read(BinaryReader br, PoolData pool)
    {
        var prevPosition = br.BaseStream.Position;
        ClipTypes mClipTypeID = (ClipTypes)br.ReadUInt32();
        br.BaseStream.Position = prevPosition;
        if(mClipTypeID == ClipTypes.eAtomic)
        {
            return new AtomicClip(br, pool);
        }
        else if(mClipTypeID == ClipTypes.eSelector)
        {
            return new SelectorClip(br);
        }
        else if(mClipTypeID == ClipTypes.eSequencer)
        {
            return new SequencerClip(br);
        }
        else if(mClipTypeID == ClipTypes.eParallel)
        {
            return new ParallelClip(br);
        }
        /*
        else if(mClipTypeID == ClipTypes.eMultiChildClip)
        {
            return new MultiChildClip(br);
        }
        */
        else if(mClipTypeID == ClipTypes.eParametric)
        {
            return new ParametricClip(br);
        }
        else if(mClipTypeID == ClipTypes.eConditionBool)
        {
            return new ConditionBoolClip(br);
        }
        else if(mClipTypeID == ClipTypes.eConditionFloat)
        {
            return new ConditionFloatClip(br);
        }
        else
            throw new Exception("Invalid clip type");
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write((uint)mClipTypeID);
    }
}

class AtomicClip : ClipData
{
    private PoolData pool;

    public uint mStartTick;
    public uint mEndTick;
    public float mTickDuration;
    public int mAnimDataIndex;
    //public AnimResourceBase? mAnimData;
    public EventResource? mEventData;
    public MaskResource? mMaskData;
    public TrackResource? mTrackData;
    public UpdaterResource? mUpdaterData;
    public string? mSyncGroupName;
    public uint mSyncGroup;
    public uint[] mExtBuffer;

    public AtomicClip(BinaryReader br, PoolData pool): base(br)
    {
        this.pool = pool;
        
        mStartTick = br.ReadUInt32();
        mEndTick = br.ReadUInt32();
        mTickDuration = br.ReadSingle();
        mAnimDataIndex = br.ReadInt32();
        long mEventDataAddr = br.ReadAddr();
        long mMaskDataAddr = br.ReadAddr();
        long mTrackDataAddr = br.ReadAddr();
        long mUpdaterDataOffset = br.ReadAddr();
        long mSyncGroupNameOffset = br.ReadAddr();
        mSyncGroup = br.ReadUInt32();
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
            br.ReadUInt32(),
        };

        var prevPosition = br.BaseStream.Position;
        //br.BaseStream.Position = mAnimDataAddr;
        //if(mAnimDataAddr != 0) mAnimData = AnimResourceBase.Read(br);
        //if(mAnimDataIndex != -1) mAnimData = pool.mAnimDataAry[mAnimDataIndex];
        br.BaseStream.Position = mEventDataAddr;
        if(mEventDataAddr != 0) mEventData = new EventResource(br);
        br.BaseStream.Position = mMaskDataAddr;
        if(mMaskDataAddr != 0) mMaskData = new MaskResource(br);
        br.BaseStream.Position = mTrackDataAddr;
        if(mTrackDataAddr != 0) mTrackData = new TrackResource(br);
        br.BaseStream.Position = mUpdaterDataOffset;
        if(mUpdaterDataOffset != 0) mUpdaterData = new UpdaterResource(br);
        br.BaseStream.Position = mSyncGroupNameOffset;
        if(mSyncGroupNameOffset != 0) mSyncGroupName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        //Debug.Assert(mAnimData == null || (pool.mAnimDataAry?.Contains(mAnimData) ?? false));
        Debug.Assert(mEventData == null || (pool.mEventDataAry?.Contains(mEventData) ?? false));
        Debug.Assert(mMaskData == null || (pool.mMaskDataAry?.Contains(mMaskData) ?? false));
        Debug.Assert(mTrackData == null || (pool.mBlendTrackAry?.Contains(mTrackData) ?? false));
    }
};

class UpdaterResource : Resource
{
    public uint mVersion;
    public ushort mNumUpdaters;
    public UpdaterData mUpdaters;
    public UpdaterResource(BinaryReader br): base(br)
    {
        
    }
};

class UpdaterData : Resource
{
    public ushort mInputType;
    public ushort mOutputType;
    public byte mNumTransforms;
    public AnimValueProcessorData[] mProcessors;
    public UpdaterData(BinaryReader br): base(br)
    {
    }
}

class AnimValueProcessorData : Resource
{
    public ushort mProcessorType;
    public AnimValueProcessorData(BinaryReader br) : base(br)
    {
        mProcessorType = br.ReadUInt16();
        br.ReadUInt16(); //TODO: Verify (alignment)
    }
}

class SelectorClip : ClipData
{
    public uint mTrackIndex;
    public uint mNumPairs;
    public SelectorClip(BinaryReader br) : base(br)
    {
        mTrackIndex = br.ReadUInt32();
        mNumPairs = br.ReadUInt32();
    }
}

// Basically SelectorClip
class SequencerClip : ClipData
{
    public uint mTrackIndex;
    public uint mNumPairs;
    public SequencerClip(BinaryReader br) : base(br)
    {
        mTrackIndex = br.ReadUInt32();
        mNumPairs = br.ReadUInt32();
    }
}

internal class ParametricClip : ClipData
{
    public uint mNumPairs;
    public uint mUpdaterType;
    public MaskResource mMaskData;
    public TrackResource mTrackData;
    public ParametricClip(BinaryReader br) : base(br)
    {
        mNumPairs = br.ReadUInt32();
        mUpdaterType = br.ReadUInt32();
        //TODO:
    }
}

internal class MultiChildClip : ClipData
{
    public MultiChildClip(BinaryReader br) : base(br)
    {
    }
}

internal class ParallelClip : ClipData
{
    public uint mClipFlag;
    public uint mNumClips;
    public ParallelClip(BinaryReader br) : base(br)
    {
        long mClipFlagPtr = br.ReadAddr();
        mNumClips = br.ReadUInt32();
    }
}
class ConditionBoolClip : ClipData
{
    public uint mTrackIndex;
    public uint mNumPairs;
    public uint mUpdaterType;
    public bool mChangeAnimationMidPlay;
    public ConditionBoolClip(BinaryReader br) : base(br)
    {
        mTrackIndex = br.ReadUInt32();
        mNumPairs = br.ReadUInt32();
        mUpdaterType = br.ReadUInt32();
        mChangeAnimationMidPlay = br.ReadUInt32() != 0; //TODO: Verify
    }
}

// Basically ConditionBoolClip
internal class ConditionFloatClip : ClipData
{
    public uint mTrackIndex;
    public uint mNumPairs;
    public uint mUpdaterType;
    public bool mChangeAnimationMidPlay;
    public ConditionFloatClip(BinaryReader br) : base(br)
    {
        mTrackIndex = br.ReadUInt32();
        mNumPairs = br.ReadUInt32();
        mUpdaterType = br.ReadUInt32();
        mChangeAnimationMidPlay = br.ReadUInt32() != 0; //TODO: Verify
    }
}