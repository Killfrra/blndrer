class AnimResourceBase : Resource
{
    public uint formatToken;
    protected AnimResourceBase(BinaryReader br): base(br)
    {
        formatToken = br.ReadUInt32();
    }
    public static AnimResourceBase Read(BinaryReader br)
    {
        long prevPosition = br.BaseStream.Position;
        uint formatToken = br.ReadUInt32();
        br.BaseStream.Position = prevPosition;
        return new AnimationResource(br);
    }
    public override void Write(BinaryWriter bw)
    {
        bw.Write(Memory.SizeOf(this));
        bw.Write(formatToken);
    }
};

class AnimationResource : AnimResourceBase
{
    uint mVersion;
    uint mFlags;
    uint mNumChannels;
    uint mNumTicks;
    float mTickDuration;
    //Riot::Offset_t mJointNameHashesOffset;
    string mAssetName;
    //Riot::Offset_t mTimeOffset;
    //Riot::Offset_t mVectorPaletteOffset;
    //Riot::Offset_t mQuatPaletteOffset;
    //Riot::Offset_t mTickDataOffset;
    uint[] mExtBuffer;
    public AnimationResource(BinaryReader br): base(br)
    {
        mVersion = br.ReadUInt32();
        mFlags = br.ReadUInt32();
        mNumChannels = br.ReadUInt32();
        mNumTicks = br.ReadUInt32();
        mTickDuration = br.ReadUInt32();
        long mJointNameHashesOffset = br.ReadAddr(baseAddr);
        long mAssetNameOffset = br.ReadAddr(baseAddr);
        long mTimeOffset = br.ReadAddr(baseAddr);
        long mVectorPaletteOffset = br.ReadAddr(baseAddr);
        long mQuatPaletteOffset = br.ReadAddr(baseAddr);
        long mTickDataOffset = br.ReadAddr(baseAddr);
        mExtBuffer = new uint[]
        {
            br.ReadUInt32(),
            br.ReadUInt32(),
            br.ReadUInt32(),
        };

        long prevPosition = br.BaseStream.Position;
        br.BaseStream.Position = mAssetNameOffset;
        mAssetName = br.ReadCString();
        br.BaseStream.Position = prevPosition;
    }
};