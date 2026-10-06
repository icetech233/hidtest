using Aula.Devices;
using Aula.Protocol;

namespace Aula.Output;

/// <summary>
/// 设备信息 / 固件版本 / 说明书 PDF 链接的中文输出。
/// </summary>
public static class DeviceInfoPrinter
{
    public static void Print(DeviceInfo info, bool wireless)
    {
        Console.WriteLine("设备信息（读取自设备）");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine($"  固件版本 : {info.BinVersion}");
        Console.WriteLine($"  VID/PID  : 0x{info.Vid:X4} / 0x{info.Pid:X4}   deviceId={info.DeviceId}");
        if (wireless)
        {
            Console.WriteLine($"  电    量 : {info.BatteryLevel}%   {(info.IsCharging ? "充电中" : "未充电")}   chargeStatus={info.ChargeStatus}");
        }
        else
        {
            Console.WriteLine($"  电    量 : 有线模式（线缆供电，视为充电中；驱动不显示电量）   chargeStatus={info.ChargeStatus}");
        }
        Console.WriteLine($"  工作模式 : 0x{info.WorkMode:X2} ({info.WorkMode})");
        Console.WriteLine($"  厂商/产品索引: {info.Manufacturer} / {info.Product}");
        Console.WriteLine($"  轴体信息 : {info.AxisInfo}   实时精度: {info.RtPrecision}");
        Console.WriteLine($"  帧缓存上限: TFT={info.TftMaxFrames} GIF={info.GifMaxFrames} LED={info.LedMaxFrames}");
        Console.WriteLine($"  其他     : tftDirection={info.TftDirection}, frameVersion={info.FrameVersion}, " +
                          $"lightingVersion={info.LightingVersion}, firmwareStatus={info.FirmwareStatus}");
        Console.WriteLine();
    }

    public static void PrintPdfLink(
        AulaKeyboardDevice definition,
        string? pdfUrl,
        ProductCatalog.FirmwareRelease? firmware)
    {
        var pdfName = ProductCatalog.GetPdfProductName(definition);
        var firmwareName = ProductCatalog.GetFirmwareProductName(definition);

        Console.WriteLine("产品说明书 / 固件（按 pId/pName 映射）");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine($"  型号          : {definition.ModelName}");
        Console.WriteLine($"  PDF 检索名    : {pdfName}");
        Console.WriteLine($"  固件检索名    : {(string.IsNullOrEmpty(firmwareName) ? "(空，网页驱动不查询)" : firmwareName)}");
        
        if (pdfUrl is not null)
        {
            Console.WriteLine($"  说明书 PDF    : {pdfUrl}");
        }
        else
        {
            Console.WriteLine("  说明书 PDF    : (云端查询失败，候选直链如下)");
            foreach (var url in ProductCatalog.GetPdfUrlCandidates(definition))
            {
                Console.WriteLine($"  候选 PDF 直链 : {url}");
            }
        }

        if (firmware is not null)
        {
            Console.WriteLine($"  最新固件版本  : {firmware.Version}");
            Console.WriteLine($"  固件下载      : {firmware.DownloadUrl}");
        }
        else if (!string.IsNullOrEmpty(firmwareName))
        {
            Console.WriteLine("  最新固件版本  : (云端查询失败)");
        }

        Console.WriteLine();
    }

    public static void PrintReadFailed(string reason)
    {
        Console.WriteLine("读取设备信息失败。");
        Console.WriteLine($"  原因: {reason}");
        Console.WriteLine();
    }
}
