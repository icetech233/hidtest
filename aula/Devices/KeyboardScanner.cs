using HidSharp;
using HidSharp.Reports;

namespace Aula.Devices;

/// <summary>
/// 扫描结果：一个被识别出来的键盘（已经过去重）。
/// 携带命中的 <see cref="HidDevice"/>，后续直接打开它，不再去试其他接口。
/// </summary>
/// <param name="Device">命中的 HID 接口（协议接口）。</param>
/// <param name="Definition">命中的设备表定义。</param>
/// <param name="UsagePage">该设备顶层集合实际的 usagePage。</param>
/// <param name="Usage">该设备顶层集合实际的 usage。</param>
/// <param name="ProductName">设备上报的产品名（可能为空）。</param>
public sealed record KeyboardHit(
    HidDevice Device,
    AulaKeyboardDevice Definition,
    int UsagePage,
    int Usage,
    string? ProductName)
{
    /// <summary>HID 设备路径。</summary>
    public string DevicePath => Device.DevicePath ?? string.Empty;
}

/// <summary>
/// 单个 HID 接口的原始信息，用于排查（未命中设备表时打印）。
/// </summary>
public sealed record HidInterfaceInfo(
    int VendorId,
    int ProductId,
    int UsagePage,
    int Usage,
    string DevicePath,
    string? ProductName);

/// <summary>
/// 键盘扫描器。
/// 对应网页驱动 JS 中 HID 服务的 init() / rescan() 逻辑：
///   navigator.hid.getDevices()
///     -&gt; devices.filter(d =&gt; d.vendorId === def.vId &amp;&amp; d.productId === def.pId
///          &amp;&amp; d.collections.some(c =&gt; c.usagePage === def.usagePage &amp;&amp; c.usage === def.usage))
///     -&gt; addDevice() 反查型号
///     -&gt; syncRef() 按 pId_vId_名称 去重
/// </summary>
public static class KeyboardScanner
{
    /// <summary>
    /// 扫描已支持的全部键盘系列（MINI60 + WIN68）。
    /// </summary>
    public static IReadOnlyList<KeyboardHit> Scan()
        => Scan(AulaKeyboardDevice.AllSeries, AulaKeyboardDevice.AllVendorIds);

    /// <summary>
    /// 扫描 MINI60 系列键盘。
    /// </summary>
    public static IReadOnlyList<KeyboardHit> ScanMini60()
        => Scan(AulaKeyboardDevice.Mini60Series, AulaKeyboardDevice.Mini60VendorIds);

    /// <summary>
    /// 扫描 WIN68 系列键盘。
    /// </summary>
    public static IReadOnlyList<KeyboardHit> ScanWin68()
        => Scan(AulaKeyboardDevice.Win68Series, AulaKeyboardDevice.Win68VendorIds);

    /// <summary>
    /// 按指定设备表扫描。
    /// </summary>
    public static IReadOnlyList<KeyboardHit> Scan(
        IReadOnlyList<AulaKeyboardDevice> definitions,
        IReadOnlyList<int>? vendorIds = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var hits = new Dictionary<string, KeyboardHit>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in EnumerateHidDevices(vendorIds ?? definitions.Select(d => d.Vid).Distinct().ToArray()))
        {
            int vendorId = device.VendorID;
            int productId = device.ProductID;

            string? productName = SafeGet(device.GetProductName);

            var definition = AulaKeyboardDevice.FindModel(definitions, vendorId, productId, productName);
            if (definition is null)
            {
                continue;
            }

            // 顶层集合（usagePage, usage）校验：同一把键盘会暴露标准键盘 / 消费者控制 /
            // 厂商自定义等多个接口，只有命中表里 usagePage+usage 的才是驱动可用接口。
            foreach (var (usagePage, usage) in ReadTopLevelUsages(device))
            {
                if (usagePage != definition.UsagePage || usage != definition.Usage)
                {
                    continue;
                }

                // JS: key = pId + '_' + vId + '_' + (customName || pName)
                var key = $"{productId}_{vendorId}_{definition.ModelName}";
                if (!hits.ContainsKey(key))
                {
                    hits[key] = new KeyboardHit(
                        device,
                        definition,
                        usagePage,
                        usage,
                        productName);
                }

                break;
            }
        }

        return hits.Values
            .OrderBy(h => h.Definition.ModelName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 直接打开扫描时命中的那个协议接口（正常路径，不需要再试其他接口）。
    /// </summary>
    public static KeyboardSession? TryOpen(KeyboardHit hit)
        => KeyboardSession.TryOpen(hit.Device);

    /// <summary>
    /// 列出指定 VID 下的全部 HID 接口（含其实际 usagePage/usage），用于未命中时排查。
    /// </summary>
    public static IReadOnlyList<HidInterfaceInfo> ListRawByVid(int vendorId)
    {
        var result = new List<HidInterfaceInfo>();

        foreach (var device in EnumerateHidDevices([vendorId]))
        {
            var usages = ReadTopLevelUsages(device).ToArray();
            int usagePage = usages.Length > 0 ? usages[0].UsagePage : -1;
            int usage = usages.Length > 0 ? usages[0].Usage : -1;

            result.Add(new HidInterfaceInfo(
                device.VendorID,
                device.ProductID,
                usagePage,
                usage,
                device.DevicePath ?? string.Empty,
                SafeGet(device.GetProductName)));
        }

        return result;
    }

    /// <summary>
    /// 找出指定 VID/PID 下所有可用的 HID 接口（不做 usage 过滤），供后续读写报告使用。
    /// </summary>
    public static IReadOnlyList<HidDevice> FindHidDevices(int vendorId, int productId)
        => DeviceList.Local.GetHidDevices(vendorId, productId).ToArray();

    private static IEnumerable<HidDevice> EnumerateHidDevices(IReadOnlyList<int> vendorIds)
    {
        var filter = vendorIds.Distinct().ToHashSet();

        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            if (filter.Contains(device.VendorID))
            {
                yield return device;
            }
        }
    }

    /// <summary>
    /// 读取设备顶层集合的 (usagePage, usage)。
    /// HidSharp 没有 HidDevice.UsagePage/Usage，必须解析报告描述符；
    /// Usages 中的值编码为 (usagePage &lt;&lt; 16) | usage。
    /// </summary>
    public static IEnumerable<(int UsagePage, int Usage)> ReadTopLevelUsages(HidDevice device)
    {
        ReportDescriptor descriptor;
        try
        {
            descriptor = device.GetReportDescriptor();
        }
        catch (Exception)
        {
            // 个别设备重建描述符会失败，跳过该设备但不中断整体扫描。
            yield break;
        }

        foreach (var item in descriptor.DeviceItems)
        {
            foreach (uint full in item.Usages.GetAllValues())
            {
                yield return ((int)(full >> 16), (int)(full & 0xFFFF));
            }
        }
    }

    private static string? SafeGet(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
