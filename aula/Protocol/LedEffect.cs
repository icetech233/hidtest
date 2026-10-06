namespace Aula.Protocol;

/// <summary>
/// 灯效数据，字段布局对应 JS：
///   $Qe = async n =&gt; {
///     const e = await jn(n, {cmd: Yt.GET_LED_EFFECT, contentSize: 16});
///     const t = new Uint8Array(e.flatMap(r =&gt; Array.from(r.slice(8)))).slice(0, 16);
///     return { mode:t[0], red:t[1], green:t[2], blue:t[3],
///              secondaryRed:t[5], secondaryGreen:t[6], secondaryBlue:t[7],
///              colorMode:t[8], brightness:t[9], speed:t[10], direction:t[11], effectModeType:t[12] };
///   }
/// </summary>
public sealed record LedEffect(
    int Mode,
    int Red,
    int Green,
    int Blue,
    int SecondaryRed,
    int SecondaryGreen,
    int SecondaryBlue,
    int ColorMode,
    int Brightness,
    int Speed,
    int Direction,
    int EffectModeType)
{
    public const int ContentSize = 16;

    /// <summary>亮度上限（JS: maxLightly = 5）。</summary>
    public const int MaxBrightness = 5;

    /// <summary>速度上限（JS: maxSpeed = 5）。</summary>
    public const int MaxSpeed = 5;

    /// <summary>灯效是否关闭（JS: mode === 0 为关灯）。</summary>
    public bool IsOff => Mode == 0;

    /// <summary>是否随机色（JS: randomColor = colorMode === 1）。</summary>
    public bool RandomColor => ColorMode == 1;

    /// <summary>方向（JS: direction === 1 为正方向）。</summary>
    public bool Forward => Direction == 1;

    /// <summary>该灯效是否支持"速度"调节（JS: speedDisabled = [1,20].includes(mode)）。</summary>
    public bool SpeedDisabled => Mode is 1 or 20;

    /// <summary>该灯效是否支持"方向"调节（JS: directionDisabled = ![10,11,12,16,18].includes(mode)）。</summary>
    public bool DirectionDisabled => Mode is not (10 or 11 or 12 or 16 or 18);

    /// <summary>该灯效是否关闭颜色调节（JS: colorClosed = [6,8,20].includes(mode)）。</summary>
    public bool ColorClosed => Mode is 6 or 8 or 20;

    /// <summary>灯效中文名（取自网页驱动多语言表 kLight.lightEffectHfd）。</summary>
    public string ModeName => ModeNameTable.TryGetValue(Mode, out var name) ? name : $"未知灯效({Mode})";

    /// <summary>
    /// mode 值 → 中文灯效名。映射关系来自 light-6B7nnya7.js 的 effectMode 列表 +
    /// index 主包多语言表 kLight.lightEffectHfd。
    /// </summary>
    public static IReadOnlyDictionary<int, string> ModeNameTable { get; } =
        new Dictionary<int, string>
        {
            [0] = "关灯",
            [1] = "静态恒亮",      // static
            [2] = "单点亮",        // dynamic1
            [3] = "单熄灭",        // dynamic2
            [4] = "繁星点点",      // dynamic3
            [5] = "漫天飞雪",      // dynamic4
            [6] = "百花争艳",      // dynamic5
            [7] = "动态呼吸",      // dynamic6
            [8] = "光谱循环",      // dynamic7
            [9] = "彩泉涌动",      // dynamic8
            [10] = "多彩纵横",     // dynamic9
            [11] = "随波逐流",     // dynamic10
            [12] = "峰回路转",     // dynamic11
            [13] = "一触即发",     // dynamic12
            [14] = "一石二鸟",     // dynamic13
            [15] = "涟漪扩散",     // dynamic14
            [16] = "川流不息",     // dynamic15
            [17] = "重峦叠嶂",     // dynamic16
            [18] = "斜风细雨",     // dynamic17
            [19] = "来回穿梭",     // dynamic18
            [20] = "自定义",       // custom
            [21] = "彩虹风车",     // dynamic19（其他型号页面使用）
            [22] = "七彩聚拢",     // dynamic20
            [23] = "霓虹叠影",     // dynamic21
        };

    public string ColorText => RandomColor ? "随机色" : $"RGB({Red}, {Green}, {Blue})";
}
