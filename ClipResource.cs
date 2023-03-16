
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
    public ClipResource(BinaryReader br): base(br)
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
        if(mClipDataOffset != 0) mClipData = ClipData.Read(br);
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
    public static ClipData Read(BinaryReader br)
    {
        var prevPosition = br.BaseStream.Position;
        ClipTypes mClipTypeID = (ClipTypes)br.ReadUInt32();
        br.BaseStream.Position = prevPosition;
        if(mClipTypeID == ClipTypes.eAtomic)
        {
            return br.Read<AtomicClip>();
        }
        else if(mClipTypeID == ClipTypes.eSelector)
        {
            return br.Read<SelectorClip>();
        }
        else if(mClipTypeID == ClipTypes.eSequencer)
        {
            return br.Read<SequencerClip>();
        }
        else if(mClipTypeID == ClipTypes.eParallel)
        {
            return br.Read<ParallelClip>();
        }
        /*
        else if(mClipTypeID == ClipTypes.eMultiChildClip)
        {
            return br.Read<MultiChildClip>();
        }
        */
        else if(mClipTypeID == ClipTypes.eParametric)
        {
            return br.Read<ParametricClip>();
        }
        else if(mClipTypeID == ClipTypes.eConditionBool)
        {
            return br.Read<ConditionBoolClip>();
        }
        else if(mClipTypeID == ClipTypes.eConditionFloat)
        {
            return br.Read<ConditionFloatClip>();
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

    public AtomicClip(BinaryReader br): base(br)
    {   
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
        if(mEventDataAddr != 0) mEventData = br.Read<EventResource>();
        br.BaseStream.Position = mMaskDataAddr;
        if(mMaskDataAddr != 0) mMaskData = br.Read<MaskResource>();
        br.BaseStream.Position = mTrackDataAddr;
        if(mTrackDataAddr != 0) mTrackData = br.Read<TrackResource>();
        br.BaseStream.Position = mUpdaterDataOffset;
        if(mUpdaterDataOffset != 0) mUpdaterData = br.Read<UpdaterResource>();
        br.BaseStream.Position = mSyncGroupNameOffset;
        if(mSyncGroupNameOffset != 0) mSyncGroupName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }

    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);

        //Debug.Assert(mAnimData == null || (pool.mAnimDataAry?.Contains(mAnimData) ?? false));
        // Debug.Assert(mAnimDataIndex >= 0 && mAnimDataIndex < pool.mAnimDataAry.Length);
        // Debug.Assert(mEventData == null || (pool.mEventDataAry?.Contains(mEventData) ?? false));
        // Debug.Assert(mMaskData == null || (pool.mMaskDataAry?.Contains(mMaskData) ?? false));
        // Debug.Assert(mTrackData == null || (pool.mBlendTrackAry?.Contains(mTrackData) ?? false));

        bw.Write(mStartTick);
        bw.Write(mEndTick);
        bw.Write(mTickDuration);

        int c() => (int)bw.BaseStream.Position;

        bw.Write(mAnimDataIndex);
        bw.Write(Memory.Allocate(c(), mEventData));
        bw.Write(Memory.Allocate(c(), mMaskData));
        bw.Write(Memory.Allocate(c(), mTrackData));
        bw.Write(Memory.Allocate(c(), mUpdaterData));
        bw.Write(Memory.Allocate(c(), mSyncGroupName));
        bw.Write(mSyncGroup);

        foreach(uint ui in mExtBuffer)
            bw.Write(ui);
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

    public override void Write(BinaryWriter bw)
    {
        throw new NotImplementedException();
    }
}
class UpdaterData : Resource
{
    public ushort mInputType;
    public ushort mOutputType;
    public byte mNumTransforms;
    public AnimValueProcessorData[] mProcessors;
    public UpdaterData(BinaryReader br): base(br)
    {
    }

    public override void Write(BinaryWriter bw)
    {
        throw new NotImplementedException();
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

    public override void Write(BinaryWriter bw)
    {
        throw new NotImplementedException();
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
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mTrackIndex);
        bw.Write(mNumPairs);
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
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mTrackIndex);
        bw.Write(mNumPairs);
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
        
        long mMaskDataAddr = br.ReadAddr();
        long mTrackDataAddr = br.ReadAddr();

        var prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mMaskDataAddr;
        if(mMaskDataAddr != 0) mMaskData = br.Read<MaskResource>();
        br.BaseStream.Position = mTrackDataAddr;
        if(mTrackDataAddr != 0) mTrackData = br.Read<TrackResource>();
        br.BaseStream.Position = prevPosition;
    }
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mNumPairs);
        bw.Write(mUpdaterType);

        int c() => (int)bw.BaseStream.Position;
        bw.Write(Memory.Allocate(c(), mMaskData));
        bw.Write(Memory.Allocate(c(), mTrackData));
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
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mClipFlag);
        bw.Write(mNumClips);
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
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mTrackIndex);
        bw.Write(mNumPairs);
        bw.Write(mUpdaterType);
        bw.Write(Convert.ToUInt32(mChangeAnimationMidPlay));
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
    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(mTrackIndex);
        bw.Write(mNumPairs);
        bw.Write(mUpdaterType);
        bw.Write(Convert.ToUInt32(mChangeAnimationMidPlay));
    }
}