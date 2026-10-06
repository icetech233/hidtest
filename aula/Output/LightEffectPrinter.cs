using Aula.Protocol;

namespace Aula.Output;

/// <summary>
/// 灯效读取结果的中文输出。
/// </summary>
public static class LightEffectPrinter
{
    public static void Print(LedEffect effect)
    {
        Console.WriteLine("灯光效果（读取自设备）");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine($"  灯效模式 : {effect.ModeName}  (mode=0x{effect.Mode:X2} / {effect.Mode})");
        Console.WriteLine($"  开关状态 : {(effect.IsOff ? "已关闭" : "已开启")}");
        Console.WriteLine($"  亮    度 : {effect.Brightness} / {LedEffect.MaxBrightness}" +
                          (effect.IsOff ? "（灯效关闭时无效）" : string.Empty));
        Console.WriteLine($"  速    度 : {effect.Speed} / {LedEffect.MaxSpeed}" +
                          (effect.SpeedDisabled ? "（该灯效不支持速度）" : string.Empty));
        Console.WriteLine($"  方    向 : {(effect.Forward ? "正向" : "反向")}" +
                          (effect.DirectionDisabled ? "（该灯效不支持方向）" : string.Empty));
        Console.WriteLine($"  颜    色 : {effect.ColorText}" +
                          (effect.ColorClosed ? "（该灯效不支持调色）" : string.Empty));

        if (!effect.RandomColor)
        {
            Console.WriteLine($"  副    色 : RGB({effect.SecondaryRed}, {effect.SecondaryGreen}, {effect.SecondaryBlue})");
        }

        Console.WriteLine($"  原始字段: colorMode={effect.ColorMode}, effectModeType={effect.EffectModeType}");
        Console.WriteLine();
    }

    public static void PrintReadFailed(string reason)
    {
        Console.WriteLine("读取灯光效果失败。");
        Console.WriteLine($"  原因: {reason}");
        Console.WriteLine();
    }

    /// <summary>诊断用：打印读到的原始输入报告。</summary>
    public static void PrintRawReports(IReadOnlyList<byte[]> reports)
    {
        if (reports.Count == 0)
        {
            Console.WriteLine("  （未收到任何输入报告）");
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"  （收到 {reports.Count} 条输入报告，取前 5 条）");
        foreach (var report in reports.Take(5))
        {
            Console.WriteLine("    " + Convert.ToHexString(report));
        }

        Console.WriteLine();
    }
}
