using System;
using System.IO;


public class VirtStream : Stream
{
    private Stream Packget;
    private long FilePos = 0;
    private long Len;

    internal VirtStream(Stream Packget, long Pos, long Len)
    {
        this.Packget = Packget;
        FilePos = Pos;
        this.Len = Len;
    }
    public override bool CanRead
    {
        get
        {
            return true;
        }
    }

    public override bool CanSeek
    {
        get
        {
            return false;
        }
    }

    public override bool CanWrite
    {
        get
        {
            return false;
        }
    }
    public override long Length
    {
        get
        {
            return Len;
        }
    }

    internal long Pos = 0;
    public override long Position
    {
        get
        {
            return Pos;
        }
        set
        {
            Seek(value, SeekOrigin.Begin);
        }
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long ReadPos = FilePos + Pos;
        if (ReadPos != Packget.Position)
            Packget.Position = ReadPos;

        if (Pos + count > Length)
            count = (int)(Length - Pos);

        int Readed = Packget.Read(buffer, offset, count);
        Pos += Readed;
        return Readed;
    }

    /// <summary>
    /// Seek the file another location
    /// </summary>
    /// <param name="offset">Value to change the pointer location</param>
    /// <param name="origin">Change from</param>
    /// <returns>New Position</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset < 0 || offset > Length)
            throw new Exception("Invalid Position");
        switch (origin)
        {
            case SeekOrigin.Begin:
                Packget.Position = FilePos + offset;
                this.Pos = offset;
                break;
            case SeekOrigin.Current:
                if (Position + offset > Length)
                    throw new Exception("Out of Range");
                Packget.Position += offset;
                this.Pos += offset;
                break;
            case SeekOrigin.End:
                long Pos = Length - offset;
                this.Pos = Pos;
                long FP = FilePos + Pos;
                if (Pos < 0)
                    throw new Exception("Out of Range");
                Packget.Position = FP;
                break;
        }
        return Pos;
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}