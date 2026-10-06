using System.Text.Json;

namespace Aula.Devices;

/// <summary>
/// 产品名映射，复刻网页驱动 version-DZbqs6RB.js 的 getPDF() / getDownloadUrl()：
/// 按 pId/pName 映射成"云端检索用产品名"，再调云端接口拿真实文件名，拼出说明书 PDF / 固件下载地址。
/// </summary>
public static class ProductCatalog
{
    /// <summary>JS 常量 Z（Fl）：说明书 PDF 前缀。pdfUrl = Z + 云端返回的文件名。</summary>
    public const string PdfBaseUrl = "https://app.aulacn.com/pdf/";

    /// <summary>JS 常量 au（Hl）：固件/素材前缀。downloadUrl = au + 云端返回的 fileName。</summary>
    public const string CommonAssetsBaseUrl = "https://app.aulacn.com/commonAssets/";

    /// <summary>网页驱动 axios 实例的 baseURL（index-ittJ_Bat.js 常量 ea：https://hubapi.aulacn.com/）。</summary>
    public const string ApiBaseUrl = "https://hubapi.aulacn.com/";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>
    /// getPDF() 里的产品检索名。
    ///   let name = 'MINI60';
    ///   switch (pId) { case 0x2723/0x2724: 'MINI68' ... case 0xfefc: pName==='MINI68HE MAX Dongle' ? 'MINI68' ... }
    /// </summary>
    public static string GetPdfProductName(AulaKeyboardDevice definition)
    {
        // 默认值（MINI60 系列、其余 Dongle 都走这里）
        var name = "MINI60";

        switch (definition.Pid)
        {
            case 0x2723 or 0x2724:
                name = "MINI68";
                break;

            case 0x2729 or 0x2742:
                if (definition.Names.Any(n => n.Contains("F75", StringComparison.OrdinalIgnoreCase)))
                {
                    name = "F75HE";
                }
                break;

            case 0x2756:
                name = "F75HE_10070";
                break;

            case 0xFEFE:
                if (HasName(definition, "MINI 68 HE PRO Dongle"))
                {
                    name = "MINI68";
                }
                else if (HasName(definition, "AULA F75 HE Dongle"))
                {
                    name = "F75HE";
                }
                break;

            case 0xFEFC:
                if (HasName(definition, "MINI68HE MAX Dongle"))
                {
                    name = "MINI68";
                }
                break;
        }

        return name;
    }

    /// <summary>
    /// getDownloadUrl() 里的产品名（用于查最新固件版本与固件包）。
    /// </summary>
    public static string GetFirmwareProductName(AulaKeyboardDevice definition)
    {
        switch (definition.Pid)
        {
            case 0x80B2 or 0x80A2:
                return "MINI 60 HE PRO";
            case 0x80A1:
                return "MINI 60 HE MAX";
            case 0x2723:
                return "MINI 68 HE PRO";
            case 0x2724:
                return "MINI 68 HE MAX";
            case 0x2729:
                return string.Empty;
            case 0x2742:
                return "NEW F75 HE";
            case 0x2756:
                return "AULA F75 HE_10070";
            case 0x8032:
                return "MINI 60 HE";
            case 0xFEFE:
                if (HasName(definition, "MINI 60 HE PRO Dongle")) return "MINI 60 HE PRO";
                if (HasName(definition, "MINI 68 HE PRO Dongle")) return "MINI 68 HE PRO";
                if (HasName(definition, "AULA F75 HE Dongle")) return "F75HE";
                return definition.ModelName;
            case 0xFEFC:
                return HasName(definition, "MINI68HE MAX Dongle") ? "MINI 68 HE MAX" : "MINI 60 HE MAX";
            default:
                return definition.ModelName;
        }
    }

    /// <summary>
    /// 说明书 PDF 链接的兜底候选（云端接口不可用时才用）。
    /// 网页驱动是"云端接口按产品名返回文件名，再拼 Z 前缀"。
    /// </summary>
    public static IReadOnlyList<string> GetPdfUrlCandidates(AulaKeyboardDevice definition)
    {
        var name = GetPdfProductName(definition);

        string[] candidates =
        [
            $"{PdfBaseUrl}{Uri.EscapeDataString(name)}.pdf",
            $"{PdfBaseUrl}{Uri.EscapeDataString(name.Replace(" ", string.Empty))}.pdf",
        ];

        return candidates.Distinct().ToArray();
    }

    /// <summary>云端返回的最新固件信息。</summary>
    public sealed record FirmwareRelease(string Version, string DownloadUrl);

    /// <summary>
    /// 复刻 getPDF()：GET /user/PDF/getFileName?pid=&amp;vid=&amp;productName=（三个参数都传产品检索名），
    /// 返回真实 PDF 文件名，拼接 Z 前缀得到完整链接。接口对应 index-DUu2k-Ps.js。
    /// </summary>
    public static async Task<string?> ResolvePdfUrlAsync(AulaKeyboardDevice definition, CancellationToken ct = default)
    {
        var name = GetPdfProductName(definition);
        var query = Uri.EscapeDataString(name);
        var url = $"{ApiBaseUrl}user/PDF/getFileName?pid={query}&vid={query}&productName={query}";

        var data = await FetchDataAsync(url, ct).ConfigureAwait(false);
        if (data is null)
        {
            return null;
        }

        var fileName = data.Value.ValueKind == JsonValueKind.String
            ? data.Value.GetString()
            : data.Value.ToString();

        return string.IsNullOrWhiteSpace(fileName) ? null : $"{PdfBaseUrl}{fileName}";
    }

    /// <summary>
    /// 复刻 getDownloadUrl()：GET /user/EXE/getFile/{产品检索名}，
    /// 返回 {version, fileName}，拼接 au 前缀得到固件下载地址。接口对应 index-FdpB5Nbp.js。
    /// </summary>
    public static async Task<FirmwareRelease?> ResolveFirmwareAsync(AulaKeyboardDevice definition, CancellationToken ct = default)
    {
        var name = GetFirmwareProductName(definition);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var url = $"{ApiBaseUrl}user/EXE/getFile/{Uri.EscapeDataString(name)}";

        var data = await FetchDataAsync(url, ct).ConfigureAwait(false);
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var version = data.Value.TryGetProperty("version", out var v) ? v.GetString() : null;
        var fileName = data.Value.TryGetProperty("fileName", out var f) ? f.GetString() : null;

        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : new FirmwareRelease(version ?? string.Empty, $"{CommonAssetsBaseUrl}{fileName}");
    }

    /// <summary>
    /// 请求云端接口并解包响应。网页驱动响应拦截器的约定（index-ittJ_Bat.js）：
    /// {code:1, data:...} 取 data；code 非 1 或结构不符视为失败。网络/解析异常一律返回 null。
    /// </summary>
    private static async Task<JsonElement?> FetchDataAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("code", out var code))
            {
                return code.ValueKind == JsonValueKind.Number && code.GetInt32() == 1 &&
                       root.TryGetProperty("data", out var data)
                    ? data.Clone()
                    : null;
            }

            return resp.IsSuccessStatusCode ? root.Clone() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool HasName(AulaKeyboardDevice definition, string expected)
        => definition.Names.Any(n => string.Equals(n, expected, StringComparison.OrdinalIgnoreCase));
}
