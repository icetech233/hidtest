using Aula.Devices;
using Aula.Protocol;

namespace Aula.Output;

/// <summary>
/// 输入报告监控：被动监听协议接口的输入上报，同时周期性发 GET_DEVICE_INFO 作对照。
/// 用于验证"充电状态是否通过推送帧上报"——网页驱动只轮询 chargeStatus（t[18]），
/// 此工具用于确认固件是否还有其他通道（如 GET_DEVICE_NOTIFY=250 / battery-change 推送）。
/// </summary>
public static class BatteryMonitor
{
    public static void Run(KeyboardHit hit, int seconds = 30)
    {
        var session = KeyboardScanner.TryOpen(hit);
        if (session is null)
        {
            Console.WriteLine("无法打开协议接口，监控失败。");
            return;
        }

        using (session)
        {
            Console.WriteLine($"监听输入报告 {seconds} 秒: {hit.DevicePath}");
            Console.WriteLine("每 3 秒发一次 GET_DEVICE_INFO 作对照；0x55 开头为 HFD 协议帧，其余为原始上报。");
            Console.WriteLine(new string('-', 60));

            var buffer = new byte[Math.Max(session.InputReportLength, session.WindowsReportLength)];
            var start = DateTime.UtcNow;
            var deadline = start.AddSeconds(seconds);
            var nextProbe = start;
            int printedReports = 0;
            int totalFrames = 0;

            while (DateTime.UtcNow < deadline)
            {
                var now = DateTime.UtcNow;

                if (now >= nextProbe)
                {
                    var payload = session.SendCommand(HfdCommand.GetDeviceInfo, DeviceInfo.ContentSize);
                    Console.WriteLine($"[{now - start:hh\\:mm\\:ss}] GET_DEVICE_INFO: " + (payload is { Length: > 18 }
                        ? $"batteryLevel={payload[17]}, chargeStatus={payload[18]}, workMode={payload[16]}"
                        : "无响应"));

                    nextProbe = now.AddSeconds(3);
                }

                // 打印命令等待期间被 WaitResponse 收下的推送帧
                var received = session.ReceivedReports;
                while (printedReports < received.Count)
                {
                    Dump(start, received[printedReports], ref totalFrames);
                    printedReports++;
                }

                var report = session.ReadReport();
                if (report is not null)
                {
                    Dump(start, report, ref totalFrames);
                }
            }

            Console.WriteLine(new string('-', 60));

            // 附加探测：GET_DEVICE_NOTIFY (0xFA) 是否可查询、有无额外数据
            foreach (var cmd in new[] { HfdCommand.GetDeviceNotify })
            {
                var notify = session.SendCommand(cmd, DeviceInfo.ContentSize);
                Console.WriteLine($"探测 cmd=0x{cmd:X2}: " + (notify is { Length: > 0 }
                    ? "响应 " + string.Join(' ', notify.Take(20).Select(b => $"{b:X2}"))
                    : "无响应"));
            }

            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"监听结束，共收到 {totalFrames} 个输入报告。");
            Console.WriteLine();
        }
    }

    private static void Dump(DateTime start, byte[] report, ref int totalFrames)
    {
        totalFrames++;

        int offset = -1;
        if (report.Length > 0 && report[0] == HfdPacket.ResponseHeader)
        {
            offset = 0;
        }
        else if (report.Length > HfdPacket.HeaderSize + 1 && report[1] == HfdPacket.ResponseHeader)
        {
            offset = 1;
        }

        var frame = string.Empty;
        if (offset >= 0)
        {
            frame = $" [HFD cmd=0x{report[offset + 1]:X2} len={report[offset + 2]}]";
        }

        var hex = string.Join(' ', report.Select(b => $"{b:X2}"));
        Console.WriteLine($"[{DateTime.UtcNow - start:hh\\:mm\\:ss}] len={report.Length}{frame} {hex}");
    }
}
