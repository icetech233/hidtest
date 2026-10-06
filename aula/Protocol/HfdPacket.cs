namespace Aula.Protocol;

/// <summary>
/// HFD 报文组装 / 解析。
/// 对应网页驱动 JS vendor 中的 x6（组包）与 TQe（解包）：
///   请求: [0]=0xAA, [1]=cmd, [2]=len, [3]=addr&amp;255, [4]=addr&gt;&gt;8, [5..7]=otherHeader, [6]=isLast, [8..]=data
///   响应: [0]=0x55, [1]=cmd, [2]=lenOrType, [3]=addr&amp;255, [4]=addr&gt;&gt;8, [8..]=data
/// </summary>
public static class HfdPacket
{
    public const byte RequestHeader = 0xAA;
    public const byte ResponseHeader = 0x55;
    public const int HeaderSize = 8;

    /// <summary>
    /// 组装一个请求报文（JS: x6(cmd, len, addr, data, reportLength, otherHeader, customHeader, isLast)）。
    /// </summary>
    public static byte[] Build(byte cmd, int length, int address, byte[]? data, int reportLength, bool isLast)
    {
        var packet = new byte[reportLength];

        packet[0] = RequestHeader;
        packet[1] = cmd;
        packet[2] = (byte)length;
        packet[3] = (byte)(address & 0xFF);
        packet[4] = (byte)((address >> 8) & 0xFF);
        // packet[5]、packet[7] 为 otherHeader，本设备不使用
        packet[6] = isLast ? (byte)0x01 : (byte)0x00;

        if (data is { Length: > 0 })
        {
            Array.Copy(data, 0, packet, HeaderSize, Math.Min(data.Length, reportLength - HeaderSize));
        }

        return packet;
    }

    /// <summary>
    /// 尝试解析响应报文（JS: TQe）。自动兼容"首字节是 Report ID"的情况。
    /// </summary>
    public static bool TryParse(byte[] report, byte expectedCommand, out byte command, out int address, out byte[] payload)
    {
        command = 0;
        address = 0;
        payload = Array.Empty<byte>();

        if (report.Length < HeaderSize + 1)
        {
            return false;
        }

        // 无 Report ID 时 0x55 在 [0]；若系统把 Report ID 放在 [0]，则 0x55 在 [1]
        int offset = -1;
        if (report[0] == ResponseHeader)
        {
            offset = 0;
        }
        else if (report.Length > HeaderSize + 1 && report[1] == ResponseHeader)
        {
            offset = 1;
        }

        if (offset < 0)
        {
            return false;
        }

        command = report[offset + 1];
        if (command != expectedCommand)
        {
            return false;
        }

        address = report[offset + 3] | (report[offset + 4] << 8);
        payload = report.Skip(offset + HeaderSize).ToArray();
        return true;
    }
}
