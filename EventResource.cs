class EventResource: Resource
{
    public uint mFormatToken;
    public uint mVersion;
    public ushort mFlags;
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
        public override void Write(BinaryWriter bw)
        {
            bw.Write(mDataID);
            bw.Write(mNameHash);
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
        public override void Write(BinaryWriter bw)
        {
            bw.Write(mDataID);
            bw.Write(mFrame);
        }
    }
    public string mName;
    private uint[] mExtBuffer;
    public EventResource(BinaryReader br): base(br)
    {
        mFormatToken = br.ReadUInt32();
        mVersion = br.ReadUInt32();
        mFlags = br.ReadUInt16();
        ushort mNumEvents = br.ReadUInt16();
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
        //ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        if(mEventOffsetArrayOffset != 0) mEventArray = br.ReadArr2(mEventOffsetArrayOffset, mNumEvents, br => BaseEventData.Read(br), baseAddr);
        br.BaseStream.Position = mEventDataOffset;
        if(mEventDataOffset != 0) mEventData = BaseEventData.Read(br);
        br.BaseStream.Position = mEventNameHashOffset;
        if(mEventNameHashOffset != 0) mEventNameHash = new EventNameHash(br);
        br.BaseStream.Position = mEventFrameOffset;
        if(mEventFrameOffset != 0) mEventFrame = new EventFrame(br);
        br.BaseStream.Position = mNameOffset;
        if(mNameOffset != 0) mName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
    public override void Write(BinaryWriter bw)
    {
        int baseAddr = (int)bw.BaseStream.Position;
        bw.Write(Memory.SizeOf(this));
        bw.Write(mFormatToken);
        bw.Write(mVersion);
        bw.Write((ushort)mFlags);
        bw.Write((ushort)mEventArray.Length);
        bw.Write(mUniqueID);

        int mEventOffsetArrayOffset;
        int mEventDataOffset;
        int mEventNameHashOffset;
        int mEventFrameOffset;
        int mNameOffset;

        bw.Write(mEventOffsetArrayOffset = Memory.AllocateArray2(baseAddr, mEventArray, baseAddr));
        bw.Write(mEventDataOffset = Memory.Allocate(baseAddr, mEventData));
        bw.Write(mEventNameHashOffset = Memory.Allocate(baseAddr, mEventNameHash));
        bw.Write(mEventFrameOffset = Memory.Allocate(baseAddr, mEventFrame));
        bw.Write(mNameOffset = Memory.Allocate(baseAddr, mName));

        foreach(uint ui in mExtBuffer)
            bw.Write(ui);
    }
}

class BaseEventData : Resource
{
    public EventType mEventTypeId;
    public enum EventType
    {
        SoundEventData = 0x1,
        ParticleEventData = 0x2,
        SubmeshVisibilityEventData = 0x3,
        FadeEventData = 0x4,
        JointSnapEventData = 0x5,
        EnableLookAtEventData = 0x6,
    }
    public uint mFlags;
    public float mFrame;
    public string? mName;
    protected BaseEventData(BinaryReader br): base(br)
    {
        mEventTypeId = (EventType)br.ReadUInt32();
        mFlags = br.ReadUInt32();
        mFrame = br.ReadSingle();
        long mNameOffset = br.ReadAddr(baseAddr);
        //ReadExtraBytes();

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mNameOffset;
        if(mNameOffset != 0) mName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
    public static BaseEventData Read(BinaryReader br)
    {
        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position += 4; // mResourceSize
        EventType mEventTypeId = (EventType)br.ReadUInt32();
        br.BaseStream.Position = prevPosition;

        if(mEventTypeId == EventType.SoundEventData)
            return new SoundEventData(br);
        if(mEventTypeId == EventType.ParticleEventData)
            return new ParticleEventData(br);
        if(mEventTypeId == EventType.SubmeshVisibilityEventData)
            return new SubmeshVisibilityEventData(br);
        if(mEventTypeId == EventType.FadeEventData)
            return new FadeEventData(br);
        if(mEventTypeId == EventType.JointSnapEventData)
            return new JointSnapEventData(br);
        if(mEventTypeId == EventType.EnableLookAtEventData)
            return new EnableLookAtEventData(br);
        else
            //return new BaseEventData(br);
            throw new Exception("Invalid event type");
    }
    public override void Write(BinaryWriter bw)
    {
        int baseAddr = (int)bw.BaseStream.Position;
        //bw.Write(Memory.SizeOf(this));
        bw.Write(0u);
        bw.Write((uint)mEventTypeId);
        bw.Write(mFlags);
        bw.Write(mFrame);
        bw.Write(Memory.Allocate(baseAddr, mName));
    }
}

internal class JointSnapEventData : BaseEventData
{
    public float mEndFrame;
    public ushort mJointToOverrideIdx;
    public ushort mJointToSnapToIdx;
    public JointSnapEventData(BinaryReader br) : base(br)
    {
        mEndFrame = br.ReadSingle();
        mJointToOverrideIdx = br.ReadUInt16();
        mJointToSnapToIdx = br.ReadUInt16();
    }
}

internal class FadeEventData : BaseEventData
{
    public float mTimeToFade;
    public float mTargetAlpha;
    public float mEndFrame;
    public FadeEventData(BinaryReader br) : base(br)
    {
        mTimeToFade = br.ReadSingle();
        mTargetAlpha = br.ReadSingle();
        mEndFrame = br.ReadSingle();
    }
}

internal class SubmeshVisibilityEventData : BaseEventData
{
    public float mEndFrame;
    public uint mShowSubmeshHash;
    public uint mHideSubmeshHash;
    public SubmeshVisibilityEventData(BinaryReader br) : base(br)
    {
        mEndFrame = br.ReadSingle();
        mShowSubmeshHash = br.ReadUInt32();
        mHideSubmeshHash = br.ReadUInt32();
    }
}

internal class ParticleEventData : BaseEventData
{
    public string? mEffectName;
    public string? mBoneName;
    public string? mTargetBoneName;
    public float mEndFrame;
    public ParticleEventData(BinaryReader br) : base(br)
    {
        long mEffectNameOffset = br.ReadAddr(baseAddr);
        long mBoneNameOffset = br.ReadAddr(baseAddr);
        long mTargetBoneNameOffset = br.ReadAddr(baseAddr);
        mEndFrame = br.ReadSingle();
        
        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mEffectNameOffset;
        if(mEffectNameOffset != 0) mEffectName = br.ReadCString();
        br.BaseStream.Position = mBoneNameOffset;
        if(mBoneNameOffset != 0) mBoneName = br.ReadCString();
        br.BaseStream.Position = mTargetBoneNameOffset;
        if(mTargetBoneNameOffset != 0) mTargetBoneName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
}

internal class SoundEventData : BaseEventData
{
    public string? mSoundName;
    public SoundEventData(BinaryReader br) : base(br)
    {
        long mSoundNameOffset = br.ReadAddr();
        
        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mSoundNameOffset;
        if(mSoundNameOffset != 0) mSoundName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
}

class EnableLookAtEventData : BaseEventData
{
    public float mEndFrame;
    public uint mEnableLookAt;
    public uint mLockCurrentValues;
    public EnableLookAtEventData(BinaryReader br) : base(br)
    {
        mEndFrame = br.ReadSingle();
        mEnableLookAt = br.ReadUInt32();
        mLockCurrentValues = br.ReadUInt32();
    }
}