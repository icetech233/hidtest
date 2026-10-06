using Aula.Protocol;
using HidSharp;

namespace Aula.Devices;

/// <summary>
/// 已打开的键盘会话：封装 HFD 协议的"发一条命令、等一个响应"。
/// 对应 JS 中的 jn()：
///   报文长度  = device.collections[0].outputReports[0].items[0].reportCount || 32
///   每包负载  = 报文长度 - 8
///   包数      = ceil(contentSize / 每包负载)
///   发送      = device.sendReport(0, packet)   —— WebHID 的 Output Report，reportId=0
///   响应      = inputreport 事件，data[0]==0x55 且 data[1]==cmd
///
/// 注意：Windows 的 OutputReportByteLength 含 1 字节 Report ID，
/// 而协议报文长度不含，因此写入时需要在前面补一个 0x00 的 Report ID。
/// </summary>
public sealed class KeyboardSession : IDisposable
{
    private readonly HidStream _stream;
    private readonly List<byte[]> _receivedReports = new();

    private KeyboardSession(HidDevice device, HidStream stream)
    {
        Device = device;
        _stream = stream;

        WindowsReportLength = device.GetMaxOutputReportLength();
        // 协议报文长度 = Windows 报告长度 - 1（Report ID）
        ReportLength = WindowsReportLength > HfdPacket.HeaderSize + 1
            ? WindowsReportLength - 1
            : 32;
    }

    public HidDevice Device { get; }

    /// <summary>Windows 报告总长度（含 Report ID 字节）。</summary>
    public int WindowsReportLength { get; }

    /// <summary>协议报文长度（不含 Report ID），JS 中的 reportCount，通常 32 或 64。</summary>
    public int ReportLength { get; set; }

    public int PayloadSize => ReportLength - HfdPacket.HeaderSize;

    public int InputReportLength => Device.GetMaxInputReportLength();

    /// <summary>诊断用：本次会话读到的所有输入报告。</summary>
    public IReadOnlyList<byte[]> ReceivedReports => _receivedReports;

    /// <summary>
    /// 监控用：读一个原始输入报告，超时 / IO 异常返回 null。
    /// </summary>
    public byte[] ReadReport(int timeoutMs = 150)
    {
        var buffer = new byte[Math.Max(InputReportLength, WindowsReportLength)];
        try
        {
            int read = _stream.Read(buffer, 0, buffer.Length);
            return read <= 0 ? null : buffer.Take(read).ToArray();
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// 候选协议报文长度（不同型号/接口可能是 32 或 64）。
    /// </summary>
    public IEnumerable<int> CandidateReportLengths()
    {
        var candidates = new List<int> { ReportLength };
        foreach (int length in new[] { 32, 64 })
        {
            if (!candidates.Contains(length))
            {
                candidates.Add(length);
            }
        }

        return candidates;
    }

    /// <summary>
    /// 依次尝试候选报文长度，直到读取成功（null 表示失败）。
    /// 统一入口，避免每个读取器各写一遍遍历逻辑。
    /// </summary>
    /// <param name="read">按指定报文长度读取一次的委托。</param>
    public T? TryReadWithCandidates<T>(Func<int, T?> read) where T : class
    {
        foreach (int length in CandidateReportLengths())
        {
            ReportLength = length;

            var result = read(length);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private bool PrefixReportId => WindowsReportLength > ReportLength;

    public static KeyboardSession? TryOpen(HidDevice device)
    {
        try
        {
            if (!device.TryOpen(out var stream))
            {
                return null;
            }

            stream.ReadTimeout = 150;
            return new KeyboardSession(device, stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 发送一条命令并收集响应负载（已去掉报告头）。失败返回 null。
    /// </summary>
    public byte[]? SendCommand(byte command, int contentSize, byte[]? data = null, int timeoutMs = 600, int retries = 2)
    {
        int packets = Math.Max(1, (int)Math.Ceiling(contentSize / (double)PayloadSize));
        var collected = new List<byte>();

        for (int i = 0; i < packets; i++)
        {
            int address = i * PayloadSize;
            int length = i == packets - 1 ? contentSize - i * PayloadSize : PayloadSize;
            bool isLast = i == packets - 1;

            byte[]? chunk = null;
            if (data is { Length: > 0 })
            {
                int start = i * PayloadSize;
                chunk = data.Skip(start).Take(Math.Min(PayloadSize, data.Length - start)).ToArray();
            }

            var packet = HfdPacket.Build(command, length, address, chunk, ReportLength, isLast);

            byte[]? response = null;
            for (int attempt = 0; attempt <= retries && response is null; attempt++)
            {
                if (!Write(packet))
                {
                    return null;
                }

                response = WaitResponse(command, timeoutMs);
            }

            if (response is null)
            {
                return null;
            }

            collected.AddRange(response);
        }

        return collected.ToArray();
    }

    private bool Write(byte[] packet)
    {
        try
        {
            var buffer = new byte[WindowsReportLength];
            int offset = PrefixReportId ? 1 : 0;
            Array.Copy(packet, 0, buffer, offset, Math.Min(packet.Length, buffer.Length - offset));
            _stream.Write(buffer);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private byte[]? WaitResponse(byte command, int timeoutMs)
    {
        var buffer = new byte[Math.Max(InputReportLength, WindowsReportLength)];
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                int read = _stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    continue;
                }

                var report = buffer.Take(read).ToArray();
                if (_receivedReports.Count < 32)
                {
                    _receivedReports.Add(report);
                }

                if (HfdPacket.TryParse(report, command, out _, out _, out var payload))
                {
                    return payload;
                }
            }
            catch (TimeoutException)
            {
                // 单次读超时，继续轮询到总超时
            }
            catch (IOException)
            {
                // 读超时，继续轮询到总超时
            }
        }

        return null;
    }

    public void Dispose() => _stream.Dispose();
}
