# web/assets JS 文件说明

对应目录：`D:\code\aicode\hidtest\web\assets`
产品：AULA 磁轴键盘（MINI 60 HE / MINI 68 HE / F75 HE 等，支持有线、2.4G dongle、无线）
技术栈：Vue 3 + WebHID（navigator.hid）+ Pinia 风格 store，业务代码均经混淆（字符串数组 + Base64 编码）。

---

## 一、业务功能模块

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `gifLight-q7LaQilP.js` | 6.6 KB | **GIF 灯效**：按帧解析 RGB 数据，构造 HID 包循环发送灯效。方法 startGifLight / stopGifLoop / resetClass |
| `light-6B7nnya7.js` | 15.6 KB | **灯光设置**：灯效模式（常亮/动态 1-18/自定义）、亮度、速度、方向、随机色、自定义灯色。设备 SDK：getLEDEffect / setLEDEffect / getCustomLEDData / setCustomLEDData |
| `trigger-BsC6uya0.js` | 24.7 KB | **磁轴触发设置**：行程、上下死区、RT 按下/松开灵敏度、触发模式、按键批量选择、校准、轴体测试。SDK：getMagneticAxisRT / setMagneticAxisRT / getAxisInfo |
| `key-BWS_Xtxv.js` | 28.9 KB | **按键设置**：改键、宏管理（macroList，M1-M100）、Fn 层级（0/1/2）、恢复默认键位 |
| `high-hAT3T2tx.js` | 35.5 KB | **高级功能**：DKS（多键行程）、MT（多击）、TGL、SOCD、RS（Rapid Trigger）、CB（组合绑定）、宏 |
| `perf-Cr-AmYo6.js` | 6.6 KB | **性能设置**：回报率（125/250/500/1000/2000/4K/8K Hz）、游戏模式、单键唤醒、自适应校准、休眠时间 |
| `version-DZbqs6RB.js` | 8.0 KB | **固件版本 / 关于**：PDF 手册地址、最新固件版本检查与下载、导出固件版本、恢复出厂设置 |
| `info-C_orDfE6.js` | 22.8 KB | **设备连接与信息管理**：HID 设备枚举/连接、有线与 dongle 区分、布局初始化、电量轮询、一键保存全部配置；按 pId 选择对应型号的设备 SDK |
| `HFD-C-eIzihS.js` | 100.6 KB | **键盘布局 / 键码映射数据**：各型号物理布局表、键码字典（HID Usage → 浏览器 code，如 Escape/F1/VolumeUp）、默认键值表、轴体类型列表（灰木轴/海皇轴等） |

## 二、API 接口模块

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `index-DUu2k-Ps.js` | 2.2 KB | **API**：GET `/user/PDF/getFileName`，参数 {pid, vid, productName}，获取 PDF 说明书文件名 |
| `index-FdpB5Nbp.js` | 2.2 KB | **API**：按产品型号获取最新固件版本信息，返回 {version, fileName}（固件版本号与固件包文件名） |

## 三、页面与 UI 组件

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `index-sCzeKWFd.js` | 16.6 KB | **灯光设置主页面**（Vue 组件）：灯效页面/模式/亮度/速度/颜色管理，通过 ins-value 动态加载 sdkPage 子组件 |
| `sdkPage-CGHW8Wzs.js` | 280.9 KB | **SDK 动态指令页面组件集合**：空格键、灯效模式、亮度、速度、取色器、自定义页等大量子组件 |
| `aulaKeyboard-BpG5fR4T.js` | 8.0 KB | **Vue 组件**：键盘布局渲染与按键选择（鼠标按下/框选，emit 选中键） |
| `aula-numInput-BL9cZ9kQ.js` | 4.7 KB | **Vue 组件**：数字输入框（基于 ElInputNumber，支持 min/max/step/小数位钳制） |
| `aula-switch-BBNaExQY.js` | 5.1 KB | **Vue 组件**：ON/OFF 开关（支持 activeValue/inActiveValue/disabled、before-change） |

## 四、第三方库 / 公共包

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `index-ittJ_Bat.js` | 2,992.6 KB | **应用主包**：公共工具函数、全局 store、路由、i18n、消息框、RGB 解析等，被各业务模块引用 |
| `vendor-ROqIirej.js` | 3,205.3 KB | **第三方依赖打包**：Vue 3 runtime 等 |
| `element-plus-Cj-rjaKX.js` | 794.4 KB | **Element Plus**：Vue UI 组件库 |
| `three-core-DziNxeDg.js` | 677.1 KB | **Three.js**：3D 引擎 |
| `bt-stats.js` | 13.4 KB | **网站访问统计脚本**（Umami 自托管）：页面浏览、点击、性能指标（TTFB/FCP/LCP/CLS/INP）、SPA 路由上报，非业务代码 |

## 五、反混淆文件（仅供阅读分析，非运行必需）

| 文件 | 大小 | 对应原文件 |
| --- | --- | --- |
| `HFD-C-eIzihS.deob.js` | 95.7 KB | `HFD-C-eIzihS.js` |
| `info-C_orDfE6.deob.js` | 20.5 KB | `info-C_orDfE6.js` |
| `light-6B7nnya7.deob.js` | 13.6 KB | `light-6B7nnya7.js` |
| `trigger-BsC6uya0.deob.js` | 22.1 KB | `trigger-BsC6uya0.js` |
| `key-BWS_Xtxv.deob.js` | 25.4 KB | `key-BWS_Xtxv.js` |
| `high-hAT3T2tx.deob.js` | 31.4 KB | `high-hAT3T2tx.js` |
| `perf-Cr-AmYo6.deob.js` | 5.9 KB | `perf-Cr-AmYo6.js` |
| `version-DZbqs6RB.deob.js` | 7.3 KB | `version-DZbqs6RB.js` |
| `vendor.deob.js` | 3,229.3 KB | `vendor-ROqIirej.js` |
| `index.deob2.js` | 2,597.2 KB | `index-ittJ_Bat.js` |
