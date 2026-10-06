using Aula.Devices;
using Aula.Protocol;

namespace Aula.Firmware;

/// <summary>
/// 设备信息读取。对应 JS：
///   ServiceDevice.getDeviceInfo() -&gt; ere(device) -&gt; jn(device, {cmd: GET_DEVICE_INFO, contentSize: 56})
///   info-C_orDfE6.js 的 getBaseInfo(): store.deviceInfo.binVersion = 'V' + info.version
/// </summary>
public static class DeviceInfoReader
{
    public static DeviceInfo? Read(KeyboardSession session, int reportLength)
    {
        session.ReportLength = reportLength;

        var payload = session.SendCommand(HfdCommand.GetDeviceInfo, DeviceInfo.ContentSize);
        if (payload is null || payload.Length < DeviceInfo.ContentSize)
        {
            return null;
        }

        return DeviceInfo.Parse(payload);
    }

    /// <summary>
    /// 依次尝试候选报文长度读取（不同接口的报文长度可能是 32 或 64）。
    /// </summary>
    public static DeviceInfo? ReadWithFallback(KeyboardSession session)
        => session.TryReadWithCandidates(length => Read(session, length));
}
