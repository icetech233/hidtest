namespace Aula.Devices;

/// <summary>
/// 键盘设备定义，结构对齐网页驱动 JS 中设备表 q1 的条目：
/// { pName:[...], pId, vId, wireless, deviceType, usage, usagePage }
/// </summary>
public sealed record AulaKeyboardDevice(
    string[] Names,   // pName：型号别名数组，JS 用 pName.includes(productName) 反查
    int Vid,          // vId
    int Pid,          // pId
    int Usage,        // usage
    int UsagePage,    // usagePage
    bool Wireless,    // wireless
    int DeviceType)   // deviceType，键盘为 1
{
    public const int KeyboardDeviceType = 0x1;

    /// <summary>首选型号名，等价于 JS 中的 pName[0]。</summary>
    public string ModelName => Names.Length > 0 ? Names[0] : string.Empty;

    /// <summary>
    /// MINI60 系列定义（从网页驱动 JS 设备表中提取的 6 条）。
    /// </summary>
    public static IReadOnlyList<AulaKeyboardDevice> Mini60Series { get; } =
    [
        // MINI 60 HE
        new AulaKeyboardDevice(
            Names: ["MINI 60 HE"],
            Vid: 0x0C45, Pid: 0x8032,
            Usage: 0x61, UsagePage: 0xFF68,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // MINI60HE MAX  —— 开发目标第一步要找的型号
        new AulaKeyboardDevice(
            Names: ["MINI60HE MAX"],
            Vid: 0x0C45, Pid: 0x80A1,
            Usage: 0x61, UsagePage: 0xFF68,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // MINI60HE MAX 无线接收器
        new AulaKeyboardDevice(
            Names: ["MINI60HE MAX Dongle"],
            Vid: 0x0C45, Pid: 0xFEFC,
            Usage: 0x01, UsagePage: 0xFF80,
            Wireless: true, DeviceType: KeyboardDeviceType),

        // MINI 60 HE PRO
        new AulaKeyboardDevice(
            Names: ["MINI 60 HE PRO"],
            Vid: 0x0C45, Pid: 0x80B2,
            Usage: 0x61, UsagePage: 0xFF68,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // MINI 60 HE PRO
        new AulaKeyboardDevice(
            Names: ["MINI 60 HE PRO"],
            Vid: 0x0C45, Pid: 0x80A2,
            Usage: 0x61, UsagePage: 0xFF68,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // MINI 60 HE PRO 无线接收器
        new AulaKeyboardDevice(
            Names: ["MINI 60 HE PRO Dongle"],
            Vid: 0x0C45, Pid: 0xFEFE,
            Usage: 0x61, UsagePage: 0xFF60,
            Wireless: true, DeviceType: KeyboardDeviceType),
    ];

    /// <summary>MINI60 系列使用的厂商 ID，用于枚举时预筛。</summary>
    public static IReadOnlyList<int> Mini60VendorIds { get; } = [0x0C45];

    /// <summary>
    /// WIN68 系列定义（从网页驱动 JS 设备表中提取的 5 条）。
    /// </summary>
    public static IReadOnlyList<AulaKeyboardDevice> Win68Series { get; } =
    [
        // WIN 68 HE PRO
        new AulaKeyboardDevice(
            Names: ["WIN 68 HE PRO"],
            Vid: 0x1CA2, Pid: 0x1901,
            Usage: 0x01, UsagePage: 0xFFA0,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // WIN 68 HE MAX
        new AulaKeyboardDevice(
            Names: ["WIN 68 HE MAX"],
            Vid: 0x1CA2, Pid: 0x1901,
            Usage: 0x01, UsagePage: 0xFFA0,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // WIN 68 HE
        new AulaKeyboardDevice(
            Names: ["WIN 68 HE"],
            Vid: 0x2E3C, Pid: 0xC365,
            Usage: 0x91, UsagePage: 0xFF1B,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // WIN 68 EVO
        new AulaKeyboardDevice(
            Names: ["WIN 68 EVO"],
            Vid: 0x38A6, Pid: 0x2909,
            Usage: 0x01, UsagePage: 0xFFB0,
            Wireless: false, DeviceType: KeyboardDeviceType),

        // WIN68HE Ultra
        new AulaKeyboardDevice(
            Names: ["WIN68HE Ultra"],
            Vid: 0x372E, Pid: 0x103E,
            Usage: 0x61, UsagePage: 0xFF60,
            Wireless: false, DeviceType: KeyboardDeviceType),
    ];

    /// <summary>WIN68 系列使用的厂商 ID，用于枚举时预筛。</summary>
    public static IReadOnlyList<int> Win68VendorIds { get; } = [0x1CA2, 0x2E3C, 0x38A6, 0x372E];

    /// <summary>当前已支持的全部键盘系列（MINI60 + WIN68）。</summary>
    public static IReadOnlyList<AulaKeyboardDevice> AllSeries { get; } =
        Mini60Series.Concat(Win68Series).ToArray();

    /// <summary>全部已支持系列的厂商 ID，用于枚举时预筛。</summary>
    public static IReadOnlyList<int> AllVendorIds { get; } =
        Mini60VendorIds.Concat(Win68VendorIds).Distinct().ToArray();

    /// <summary>
    /// 反查型号，等价于 JS addDevice 中的：
    /// q1.filter(x =&gt; x.pId === productId &amp;&amp; x.vId === vendorId &amp;&amp; x.pName.includes(productName))
    /// </summary>
    public static AulaKeyboardDevice? FindModel(int vendorId, int productId, string? productName)
        => FindModel(AllSeries, vendorId, productId, productName);

    /// <summary>
    /// 反查型号，等价于 JS addDevice 中的：
    /// q1.filter(x =&gt; x.pId === productId &amp;&amp; x.vId === vendorId &amp;&amp; x.pName.includes(productName))
    /// </summary>
    public static AulaKeyboardDevice? FindModel(
        IReadOnlyList<AulaKeyboardDevice> table,
        int vendorId,
        int productId,
        string? productName)
    {
        var candidates = table
            .Where(d => d.Vid == vendorId && d.Pid == productId)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Names.Any(n => string.Equals(n, productName, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }
        }

        return candidates[0];
    }
}
