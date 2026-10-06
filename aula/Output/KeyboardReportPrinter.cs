using Aula.Devices;

namespace Aula.Output;

/// <summary>
/// 控制台输出：把扫描结果渲染成中文文本。
/// </summary>
public static class KeyboardReportPrinter
{
    public static void PrintScanResult(IReadOnlyList<KeyboardHit> hits)
    {
        if (hits.Count == 0)
        {
            Console.WriteLine("未发现 MINI60 系列键盘。");
            return;
        }

        Console.WriteLine($"共识别到 {hits.Count} 台设备：");
        Console.WriteLine();

        for (int i = 0; i < hits.Count; i++)
        {
            PrintHit(i + 1, hits[i]);
        }
    }

    /// <summary>
    /// 未命中设备表时的排查输出：列出指定 VID 下的所有 HID 接口及其真实 usagePage/usage。
    /// </summary>
    public static void PrintRawInterfaces(int vendorId, IReadOnlyList<HidInterfaceInfo> interfaces)
    {
        Console.WriteLine();
        Console.WriteLine($"以下为当前系统中所有 VID=0x{vendorId:X4} 的 HID 接口，可用于排查：");

        if (interfaces.Count == 0)
        {
            Console.WriteLine($"  （没有找到 VID=0x{vendorId:X4} 的任何 HID 设备）");
            return;
        }

        for (int i = 0; i < interfaces.Count; i++)
        {
            var it = interfaces[i];
            Console.WriteLine($"  [{i + 1}] VID=0x{it.VendorId:X4} PID=0x{it.ProductId:X4} " +
                              $"usagePage=0x{Hex(it.UsagePage)} usage=0x{Hex(it.Usage)} " +
                              $"名称={it.ProductName ?? "(未知)"}");
            Console.WriteLine($"       路径: {it.DevicePath}");
        }
    }

    private static void PrintHit(int index, KeyboardHit hit)
    {
        var def = hit.Definition;

        Console.WriteLine($"[{index}] 型号: {def.ModelName}");
        Console.WriteLine($"    VID=0x{def.Vid:X4}  PID=0x{def.Pid:X4}");
        Console.WriteLine($"    usagePage=0x{hit.UsagePage:X4}  usage=0x{hit.Usage:X4}");
        Console.WriteLine($"    无线: {(def.Wireless ? "是" : "否")}   设备类型: 键盘");
        Console.WriteLine($"    产品名: {hit.ProductName ?? "(未知)"}");
        Console.WriteLine($"    路径: {hit.DevicePath}");
        Console.WriteLine();
    }

    private static string Hex(int value) => value < 0 ? "????" : value.ToString("X4");
}
