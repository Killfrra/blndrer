using System.Diagnostics;
using System.Text;

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


    private static BinaryReader? prevBR; //HACK:
    private static Dictionary<long, object> cache = new();
    public static T Read<T>(this BinaryReader br)
    {
        if(prevBR != br)
            cache.Clear();
        prevBR = br;

        var pos = br.BaseStream.Position;
        var readed = cache.GetValueOrDefault(pos);
        if(readed != null)
        {
            Debug.Assert(readed.GetType() == typeof(T));
            //Console.WriteLine($"{pos} {typeof(T)} cached");
            return (T)readed;
        }
        readed = Activator.CreateInstance(typeof(T), new object?[]{ br })!;
        cache[pos] = readed;
        return (T)readed;
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