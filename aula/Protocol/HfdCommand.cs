namespace Aula.Protocol;

/// <summary>
/// HFD 协议命令字，完整对应网页驱动 JS vendor 中的常量表 Yt：
/// Yt={COMMUNICATION_START:1,...,GET_LED_EFFECT:19,...,SET_LED_EFFECT:35,...}
/// </summary>
public static class HfdCommand
{
    public const byte CommunicationStart = 0x01;
    public const byte CommunicationEnd = 0x02;
    public const byte SetFactoryReset = 0x0F;

    public const byte GetDeviceInfo = 0x10;
    public const byte GetGameMode = 0x11;
    public const byte GetKey = 0x12;
    public const byte GetLedEffect = 0x13;
    public const byte GetCustomLedData = 0x14;
    public const byte GetMacro = 0x15;
    public const byte GetFnKey = 0x16;
    public const byte GetMagneticAxisRt = 0x17;
    public const byte GetMagneticAxisDksData = 0x18;
    public const byte GetLightBox = 0x1B;
    public const byte GetDefaultFnKeyMatrix = 0x1C;
    public const byte GetSideLight = 0x1D;
    public const byte GetDefaultKeyMatrix = 0x1F;

    public const byte SetGameMode = 0x21;
    public const byte SetKey = 0x22;
    public const byte SetLedEffect = 0x23;
    public const byte SetCustomLedData = 0x24;
    public const byte SetMacro = 0x25;
    public const byte SetFnKey = 0x26;
    public const byte SetMagneticAxisRt = 0x27;
    public const byte SetMagneticAxisDksData = 0x28;
    public const byte SetDotMatrixMode = 0x2A;
    public const byte SetLightBox = 0x2B;
    public const byte SetSideLight = 0x2D;

    public const byte GetLedData = 0x32;
    public const byte GetAllLightsRgb = 0x33;

    public const byte GetDeviceNotify = 0xFA;
}
