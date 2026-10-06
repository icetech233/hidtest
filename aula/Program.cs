using Aula.Devices;
using Aula.Firmware;
using Aula.Lighting;
using Aula.Output;

Console.WriteLine("AULA 键盘扫描（MINI60 / WIN68 系列）");
Console.WriteLine("========================================");

var hits = KeyboardScanner.Scan();

KeyboardReportPrinter.PrintScanResult(hits);

if (hits.Count == 0)
{
    foreach (var vid in AulaKeyboardDevice.AllVendorIds)
    {
        KeyboardReportPrinter.PrintRawInterfaces(vid, KeyboardScanner.ListRawByVid(vid));
    }

    return;
}

var hit = hits[0];

Console.WriteLine("连接设备读取信息...");
Console.WriteLine();

// 扫描阶段已经定位到协议接口（usagePage + usage 命中的那个），直接打开它
var session = KeyboardScanner.TryOpen(hit)
              ?? FallbackOpenOtherInterface(hit);

if (session is null)
{
    Console.WriteLine("没有可用的协议接口，无法读取设备信息。");
    return;
}

using (session)
{
    Console.WriteLine($"协议接口: {session.Device.DevicePath}");
    Console.WriteLine($"报告长度: 协议报文={session.ReportLength}, Windows={session.WindowsReportLength}, 输入={session.InputReportLength}");
    Console.WriteLine();

    var deviceInfo = DeviceInfoReader.ReadWithFallback(session);
    if (deviceInfo is null)
    {
        DeviceInfoPrinter.PrintReadFailed("命令 0x10(GET_DEVICE_INFO) 无响应");
    }
    else
    {
        DeviceInfoPrinter.Print(deviceInfo, hit.Definition.Wireless);

        Console.WriteLine("查询云端说明书 / 固件...");
        var pdfUrlTask = ProductCatalog.ResolvePdfUrlAsync(hit.Definition);
        var firmwareTask = ProductCatalog.ResolveFirmwareAsync(hit.Definition);
        await Task.WhenAll(pdfUrlTask, firmwareTask);

        DeviceInfoPrinter.PrintPdfLink(hit.Definition, pdfUrlTask.Result, firmwareTask.Result);
    }

    var effect = LightEffectReader.ReadWithFallback(session);
    if (effect is not null)
    {
        LightEffectPrinter.Print(effect);
    }
}

/// <summary>
/// 兜底：已识别的接口打不开时（例如被系统占用），才去试同 VID/PID 下的其他接口。
/// </summary>
static KeyboardSession? FallbackOpenOtherInterface(KeyboardHit hit)
{
    Console.WriteLine($"已识别接口打开失败，回退扫描 0x{hit.Definition.Vid:X4}/0x{hit.Definition.Pid:X4} 下的其他 HID 接口...");
    Console.WriteLine();

    foreach (var device in KeyboardScanner.FindHidDevices(hit.Definition.Vid, hit.Definition.Pid))
    {
        if (string.Equals(device.DevicePath, hit.DevicePath, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var candidate = KeyboardSession.TryOpen(device);
        if (candidate is null)
        {
            Console.WriteLine($"  打开失败，跳过: {device.DevicePath}");
            continue;
        }

        var probe = DeviceInfoReader.ReadWithFallback(candidate);
        if (probe is null)
        {
            Console.WriteLine($"  不是协议接口，跳过: {device.DevicePath}");
            candidate.Dispose();
            continue;
        }

        Console.WriteLine($"  回退命中: {device.DevicePath}");
        Console.WriteLine();
        return candidate;
    }

    Console.WriteLine();
    return null;
}
