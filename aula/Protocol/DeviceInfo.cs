using System.Globalization;

namespace Aula.Protocol;

/// <summary>
/// 设备信息，字段布局对应 JS vendor 中的 ere（GET_DEVICE_INFO，cmd 16，contentSize 56）：
///   ere = async n =&gt; {
///     const e = await jn(n, {cmd: Yt.GET_DEVICE_INFO, contentSize: 56});
///     const t = new Uint8Array(e.flatMap(r =&gt; Array.from(r.slice(8)))).slice(0, 56);
///     return { vid:t[4]|t[5]&lt;&lt;8, pid:t[6]|t[7]&lt;&lt;8,
///              version:parseFloat((((t[8]&amp;15)+((t[8]&amp;240)&gt;&gt;4)*10+t[9]*100)/100).toFixed(2)),
///              manufacturer:t[12]|t[13]&lt;&lt;8, product:t[14]|t[15]&lt;&lt;8,
///              workMode:t[16], batteryLevel:t[17], chargeStatus:t[18],
///              axisInfo:t[20]|t[21]&lt;&lt;8, tftMaxFrames:t[22]|t[23]&lt;&lt;8,
///              gifMaxFrames:t[24]|t[25]&lt;&lt;8, ledMaxFrames:t[26]|t[27]&lt;&lt;8,
///              tftDirection:t[28], rtPrecision:t[29],
///              frameVersion:t[30], lightingVersion:t[31], firmwareStatus:t[32] };
///   }
/// </summary>
public sealed record DeviceInfo(
    int Vid,
    int Pid,
    double Version,
    int Manufacturer,
    int Product,
    int WorkMode,
    int BatteryLevel,
    int ChargeStatus,
    int AxisInfo,
    int TftMaxFrames,
    int GifMaxFrames,
    int LedMaxFrames,
    int TftDirection,
    int RtPrecision,
    int FrameVersion,
    int LightingVersion,
    int FirmwareStatus)
{
    public const int ContentSize = 56;

    /// <summary>
    /// 当前固件版本（JS: store.deviceInfo.binVersion = 'V' + info.version）。
    /// </summary>
    public string BinVersion => "V" + Version.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// JS pollBattery()（仅无线模式轮询）：isCharging = [1,2].includes(chargeStatus)。
    /// </summary>
    public bool IsCharging => ChargeStatus is 1 or 2;

    /// <summary>
    /// JS getBaseInfo()：isCharging = [1,2].includes(chargeStatus) || !wireless。
    /// 有线模式恒视为充电中（线缆供电），且此时驱动不显示电量。
    /// </summary>
    public bool IsChargingFor(bool wireless) => IsCharging || !wireless;

    /// <summary>JS: deviceId = pid + '_' + vid。</summary>
    public string DeviceId => $"{Pid}_{Vid}";

    /// <summary>
    /// 解析 56 字节负载（已去掉 8 字节报文头）。
    /// </summary>
    public static DeviceInfo Parse(byte[] p)
    {
        int Byte(int i) => i < p.Length ? p[i] : 0;
        int Word(int i) => i + 1 < p.Length ? p[i] | (p[i + 1] << 8) : p[i];

        // BCD 版本号：低 4 位 = 个位，高 4 位 = 十位，t[9] = 百位，除以 100 保留两位小数
        double version = ((Byte(8) & 0x0F) + (((Byte(8) & 0xF0) >> 4) * 10) + Byte(9) * 100) / 100.0;

        return new DeviceInfo(
            Vid: Word(4),
            Pid: Word(6),
            Version: version,
            Manufacturer: Word(12),
            Product: Word(14),
            WorkMode: Byte(16),
            BatteryLevel: Byte(17),
            ChargeStatus: Byte(18),
            AxisInfo: Word(20),
            TftMaxFrames: Word(22),
            GifMaxFrames: Word(24),
            LedMaxFrames: Word(26),
            TftDirection: Byte(28),
            RtPrecision: Byte(29),
            FrameVersion: Byte(30),
            LightingVersion: Byte(31),
            FirmwareStatus: Byte(32));
    }
}
