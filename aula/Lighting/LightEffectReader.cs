using Aula.Devices;
using Aula.Protocol;

namespace Aula.Lighting;

/// <summary>
/// 灯效读取。对应 JS：
///   ServiceDevice.getLEDEffect() -&gt; $Qe(device) -&gt; jn(device, {cmd: GET_LED_EFFECT, contentSize: 16})
/// </summary>
public static class LightEffectReader
{
    public static LedEffect? Read(KeyboardSession session, int reportLength)
    {
        session.ReportLength = reportLength;

        var payload = session.SendCommand(HfdCommand.GetLedEffect, LedEffect.ContentSize);
        if (payload is null || payload.Length < LedEffect.ContentSize)
        {
            return null;
        }

        return Parse(payload);
    }

    /// <summary>
    /// 依次尝试候选报文长度读取（不同型号/接口的报文长度可能是 32 或 64）。
    /// </summary>
    public static LedEffect? ReadWithFallback(KeyboardSession session)
        => session.TryReadWithCandidates(length => Read(session, length));

    /// <summary>
    /// 按 JS 的字段布局解析 16 字节灯效数据。
    /// </summary>
    public static LedEffect Parse(byte[] payload)
    {
        byte At(int index) => index < payload.Length ? payload[index] : (byte)0;

        return new LedEffect(
            Mode: At(0),
            Red: At(1),
            Green: At(2),
            Blue: At(3),
            SecondaryRed: At(5),
            SecondaryGreen: At(6),
            SecondaryBlue: At(7),
            ColorMode: At(8),
            Brightness: At(9),
            Speed: At(10),
            Direction: At(11),
            EffectModeType: At(12));
    }
}
