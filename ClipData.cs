
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
        mClipData = ClipData.Read(br, baseAddr);
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
    public ClipData(BinaryReader br, long baseAddr)
    {
        this.baseAddr = br.BaseStream.Position;
        //this.baseAddr = baseAddr;
        mClipTypeID = (ClipTypes)br.ReadUInt32();
    }
    public static ClipData Read(BinaryReader br, long baseAddr)
    {
        var prevPosition = br.BaseStream.Position;
        ClipTypes mClipTypeID = (ClipTypes)br.ReadUInt32();
        br.BaseStream.Position = prevPosition;
        if(mClipTypeID == ClipTypes.eAtomic)
        {
            return new AtomicClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eSelector)
        {
            return new SelectorClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eSequencer)
        {
            return new SequencerClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eParallel)
        {
            return new ParallelClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eMultiChildClip)
        {
            //return new MultiChildClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eParametric)
        {
            return new ParametricClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eConditionBool)
        {
            return new ConditionBoolClip(br, baseAddr);
        }
        else if(mClipTypeID == ClipTypes.eConditionFloat)
        {
            return new ConditionFloatClip(br, baseAddr);
        }
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
    public AnimResourceBase mAnimData;
    public uint mAnimDataIndex;
    public EventResource mEventData;
    public MaskResource mMaskData;
    public TrackResource mTrackData;
    public UpdaterResource mUpdaterData;
    public string mSyncGroupName;
    public uint mSyncGroup;
    public uint[] mExtBuffer;

    public AtomicClip(BinaryReader br, long baseAddr): base(br, baseAddr)
    {
        mStartTick = br.ReadUInt32();
        mEndTick = br.ReadUInt32();
        mTickDuration = br.ReadSingle();
        long mAnimDataAddr = br.ReadAddr(this.baseAddr);
        mAnimDataIndex = br.ReadUInt32();
        long mEventDataAddr = br.ReadAddr(this.baseAddr);
        long mMaskDataAddr = br.ReadAddr(this.baseAddr);
        long mTrackDataAddr = br.ReadAddr(this.baseAddr);
        long mUpdaterDataOffset = br.ReadAddr(this.baseAddr);
        long mSyncGroupNameOffset = br.ReadAddr(this.baseAddr);
        mSyncGroup = br.ReadUInt32();
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
            br.ReadUInt32(),
        };

        var prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mAnimDataAddr;
        if(mAnimDataAddr != 0) mAnimData = new AnimResourceBase(br);
        br.BaseStream.Position = mEventDataAddr;
        //if(mEventDataAddr != 0) mEventData = new EventResource(br);
        br.BaseStream.Position = mMaskDataAddr;
        //if(mMaskDataAddr != 0) mMaskData = new MaskResource(br);
        br.BaseStream.Position = mTrackDataAddr;
        //if(mTrackDataAddr != 0) mTrackData = new TrackResource(br);
        br.BaseStream.Position = mUpdaterDataOffset;
        //if(mUpdaterDataOffset != 0) mUpdaterData = new UpdaterResource(br);
        br.BaseStream.Position = mSyncGroupNameOffset;
        //if(mSyncGroupNameOffset != 0) mSyncGroupName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
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
    public SelectorClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
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
    public SequencerClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
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
    public ParametricClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
    {
        mNumPairs = br.ReadUInt32();
        mUpdaterType = br.ReadUInt32();
        //TODO:
    }
}

internal class MultiChildClip : ClipData
{
    public MultiChildClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
    {
    }
}

internal class ParallelClip : ClipData
{
    public uint mClipFlag;
    public uint mNumClips;
    public ParallelClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
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
    public ConditionBoolClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
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
    public ConditionFloatClip(BinaryReader br, long baseAddr) : base(br, baseAddr)
    {
        mTrackIndex = br.ReadUInt32();
        mNumPairs = br.ReadUInt32();
        mUpdaterType = br.ReadUInt32();
        mChangeAnimationMidPlay = br.ReadUInt32() != 0; //TODO: Verify
    }
}