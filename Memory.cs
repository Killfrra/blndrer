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