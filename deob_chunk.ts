#!/usr/bin/env bun
/**
 * 通用 javascript-obfuscator 反混淆：解出字符串数组，把 X(0xNN) 调用替换回明文字符串。
 *
 * 用法:
 *   bun run deob_chunk.ts <js文件> [输出文件]
 *   bun run deob_chunk.ts web/assets/light-6B7nnya7.js
 *   bun run deob_chunk.ts web/assets/index-ittJ_Bat.js web/assets/index.deob2.js
 *
 * 支持两种混淆形态：
 *   1) _0x 风格（主包与各功能分块）：
 *        function _0x1b46(){const _0x3d4f21=[...]; ...}
 *        function _0x3c19(a,b){a=a-0x1cb; var x=_0x1b46(); ...}
 *   2) 短名风格（vendor 包）：
 *        function IV(){const n=[...]; return (IV=function(){return n})()}
 *        function yT(n,e){const t=IV(); return (yT=function(r,i){return t[r-=217]})(n,e)}
 *      vendor 里有几十组独立的"数组+解码器"，本脚本会全部解出来。
 */

type Decoder = (index: number) => unknown;

let DEBUG = false;

/** 是否允许单字符别名（vendor 里常见 const e=Bn; ... e(511)）。有一定误替换风险，需手动开启。 */
let ALLOW_SHORT = false;

interface HolderInfo {
  name: string;
  source: string;
  iife: string;
}

interface DecoderInfo {
  name: string;
  source: string;
  holder: HolderInfo;
}

const CH = {
  backslash: 92,
  singleQuote: 39,
  doubleQuote: 34,
  backtick: 96,
  openBrace: 123,
  closeBrace: 125,
} as const;

/** 从 start 处的函数定义开始，做括号配对（跳过字符串字面量），返回完整函数源码。 */
function extractFunction(src: string, start: number): string {
  let i = src.indexOf("{", start);
  let depth = 1;
  let quote = 0;
  let escaped = false;

  for (i += 1; i < src.length && depth > 0; i++) {
    const c = src.charCodeAt(i);

    if (quote !== 0) {
      if (escaped) {
        escaped = false;
      } else if (c === CH.backslash) {
        escaped = true;
      } else if (c === quote) {
        quote = 0;
      }
      continue;
    }

    if (c === CH.singleQuote || c === CH.doubleQuote || c === CH.backtick) {
      quote = c;
      continue;
    }

    if (c === CH.openBrace) {
      depth += 1;
    } else if (c === CH.closeBrace) {
      depth -= 1;
    }
  }

  return src.slice(start, i);
}

/** 从 start 处的 `(function...` 开始，切出整个立即执行表达式（到分号为止）。 */
function extractIife(src: string, start: number): string {
  const body = extractFunction(src, start);
  const semicolon = src.indexOf(";", start + body.length);
  return src.slice(start, semicolon < 0 ? start + body.length : semicolon + 1);
}

function isUsableName(name: string): boolean {
  if (!/^[A-Za-z_$][\w$]*$/.test(name)) {
    return false;
  }

  // 单字符名（如 o、n、e）在压缩代码里极易与普通变量冲突，默认跳过以免误替换
  return name.length >= 2 || ALLOW_SHORT;
}

/** 1) 找出所有字符串数组持有者 function HOLDER(){var ARR=[...] ...} */
function findHolders(src: string): Map<string, HolderInfo> {
  const holders = new Map<string, HolderInfo>();
  const re = /function (\w+)\(\)\{(?:var|const|let) (\w+)=\[/g;

  for (const m of src.matchAll(re)) {
    const name = m[1];
    if (holders.has(name)) {
      continue;
    }

    const start = src.indexOf(`function ${name}(){`);
    if (start < 0) {
      continue;
    }

    const source = extractFunction(src, start);
    const after = start + source.length;
    const iife = src.slice(after).startsWith("(function")
      ? extractIife(src, after)
      : "";

    holders.set(name, { name, source, iife });
  }

  return holders;
}

/** 2a) 短名风格解码器：function N(a,b){const c=ARR();return N=function(d,e){return c[d-=OFF]}(a,b)} */
function findCanonicalDecoders(src: string, holders: Map<string, HolderInfo>): DecoderInfo[] {
  const found: DecoderInfo[] = [];
  let headHits = 0;
  let noHolder = 0;
  let tailFail = 0;

  // 第一段：function NAME(a,b){const C=ARR();
  const head = /function (\w+)\((\w+),(\w+)\)\s*\{\s*const (\w+)=(\w+)\(\);/g;

  for (const m of src.matchAll(head)) {
    headHits += 1;

    const name = m[1];
    const arrayVar = m[4];
    const arrayFn = m[5];
    const holder = holders.get(arrayFn);
    if (!holder) {
      noHolder += 1;
      continue;
    }

    // 第二段：紧跟着的 return(NAME=function(d,e){return C[d-=OFF]}
    // 这里不用正则反向引用，直接按名字拼，避免 \1 在长正则里失配
    const tail = new RegExp(
      `^\\s*return\\s*\\(?\\s*${name}\\s*=\\s*function\\(\\w+,\\w+\\)\\s*\\{\\s*return ${arrayVar}\\[\\w+-=(?:0x[0-9a-f]+|[0-9]+)\\]\\}`,
    );

    const tailStart = m.index + m[0].length;
    if (!tail.test(src.slice(tailStart))) {
      tailFail += 1;
      if (DEBUG && tailFail <= 2) {
        console.log(`[debug] tail 正则: ${tail.source}`);
        console.log(`[debug] tail 文本: ${JSON.stringify(src.slice(tailStart, tailStart + 90))}`);
      }
      continue;
    }

    const start = src.indexOf(`function ${name}(`, Math.max(0, m.index));
    if (start < 0) {
      continue;
    }

    found.push({ name, source: extractFunction(src, start), holder });
  }

  if (DEBUG) {
    console.log(`[debug] canonical: head=${headHits} 无数组=${noHolder} 尾部不匹配=${tailFail} 命中=${found.length}`);
  }

  return found;
}

/** 2b) _0x 风格解码器：函数体内调用了某个持有者（HOLDER()） */
function findLegacyDecoder(src: string, holders: Map<string, HolderInfo>): DecoderInfo | null {
  const re = /function (_0x[0-9a-f]+)\(_0x[0-9a-f]+,_0x[0-9a-f]+\)\{/g;

  for (const m of src.matchAll(re)) {
    const body = extractFunction(src, m.index);

    for (const holder of holders.values()) {
      if (!body.includes(`${holder.name}()`)) {
        continue;
      }

      // 旋转 IIFE：形如 (function(a,b){...}(HOLDER,0xNNNN));
      let iife = holder.iife;
      const marker = src.indexOf(`(${holder.name},0x`);
      if (marker >= 0) {
        const start = src.lastIndexOf("(function", marker);
        if (start >= 0) {
          iife = extractIife(src, start);
        }
      }

      return {
        name: m[1],
        source: body,
        holder: { ...holder, iife },
      };
    }
  }

  return null;
}

/** 代码片段能否独立通过语法检查（用于剔除切片切歪了的内容）。 */
function isSyntaxOk(code: string): boolean {
  try {
    new Function(code);
    return true;
  } catch {
    return false;
  }
}

/** 3) 在沙箱里执行"持有者 + 旋转 IIFE + 解码器"，拿到真实的解码函数 */
function buildDecoders(src: string): Map<string, Decoder> {
  const holders = findHolders(src);
  const decoders = findCanonicalDecoders(src, holders);
  const legacy = findLegacyDecoder(src, holders);

  if (DEBUG) {
    console.log(`[debug] 数组持有者 ${holders.size} 个`);
    console.log(`[debug] 短名解码器 ${decoders.length} 个: ${decoders.slice(0, 5).map((d) => d.name).join(",")}`);
    console.log(`[debug] _0x 解码器: ${legacy?.name ?? "(无)"}`);
  }

  if (legacy && !decoders.some((d) => d.name === legacy.name)) {
    decoders.push(legacy);
  }

  // 单字符名（如 o、n）在压缩代码里极易与普通变量冲突，跳过以免误替换
  const usable = decoders.filter(
    (d) =>
      isUsableName(d.name) &&
      isSyntaxOk(d.source) &&
      isSyntaxOk(d.holder.source) &&
      (d.holder.iife === "" || isSyntaxOk(d.holder.iife)),
  );

  if (DEBUG) {
    console.log(`[debug] 语法校验后可用解码器: ${usable.length} / ${decoders.length}`);
  }

  if (usable.length === 0) {
    throw new Error("没有找到可用的字符串解码器");
  }

  const holderNames = new Set<string>();
  const parts: string[] = [];
  for (const d of usable) {
    if (!holderNames.has(d.holder.name)) {
      holderNames.add(d.holder.name);
      parts.push(d.holder.source);
      if (d.holder.iife) {
        parts.push(d.holder.iife);
      }
    }
  }
  for (const d of usable) {
    parts.push(d.source);
  }

  const names = usable.map((d) => d.name);
  const sandbox = `${parts.join("\n")}\nreturn {${names.map((n) => `${n}:${n}`).join(",")}};`;

  let factory: () => Record<string, Decoder>;
  try {
    factory = new Function(sandbox) as () => Record<string, Decoder>;
  } catch (error) {
    if (DEBUG) {
      const lines = sandbox.split("\n");
      const at = Number(/<parse> \((\d+):/.exec(String((error as Error).stack))?.[1] ?? 1);
      for (let i = Math.max(0, at - 3); i < Math.min(lines.length, at + 2); i++) {
        console.log(`[debug] 第 ${i + 1} 行: ${lines[i].slice(0, 160)}`);
      }
    }
    throw error;
  }

  const table = factory();
  const result = new Map<string, Decoder>();
  for (const n of names) {
    const fn = table[n];
    if (typeof fn === "function") {
      result.set(n, fn);
    }
  }

  if (DEBUG) {
    for (const [n, fn] of [...result].slice(0, 5)) {
      console.log(`[debug] ${n}(210)=${JSON.stringify(fn(210))} ${n}(300)=${JSON.stringify(fn(300))}`);
    }
  }

  return result;
}

/**
 * 4) 收集解码器别名（var/const/let A=B; 的传递闭包）。
 * 每个别名都指向它对应的那个解码函数，因为一个文件里可能有多套数组。
 */
function collectAliases(src: string, decoders: Map<string, Decoder>): Map<string, Decoder> {
  const table = new Map<string, Decoder>(decoders);

  for (let round = 0; round < 8; round++) {
    let grew = false;

    // 注意结尾既可能是分号也可能是逗号：var a=B,c=D;
    for (const m of src.matchAll(/(?:var|const|let) ([A-Za-z_$][\w$]*)=([A-Za-z_$][\w$]*)\s*[,;]/g)) {
      const alias = m[1];
      const target = m[2];
      const decoder = table.get(target);

      if (decoder && isUsableName(alias) && !table.has(alias)) {
        table.set(alias, decoder);
        grew = true;
      }
    }

    if (!grew) {
      break;
    }
  }

  return table;
}

async function main(): Promise<void> {
  const args = process.argv.slice(2);

  if (args.length === 0 || args[0] === "-h" || args[0] === "--help") {
    console.log("用法: bun run deob_chunk.ts <js文件> [输出文件]");
    process.exit(1);
  }

  DEBUG = args.includes("--debug");
  ALLOW_SHORT = args.includes("--unsafe");
  const positional = args.filter((a) => !a.startsWith("--"));

  if (positional.length === 0) {
    console.log("用法: bun run deob_chunk.ts <js文件> [输出文件] [--debug] [--unsafe]");
    process.exit(1);
  }

  const file = positional[0];
  const t = await Bun.file(file).text();

  const decoders = buildDecoders(t);
  const table = collectAliases(t, decoders);

  // 索引既可能是十六进制（_0x 风格：_0x59cf(0x1a9)）也可能是十进制（vendor 风格：Bn(511)）
  const callRe = /([A-Za-z_$][\w$]*)\((0x[0-9a-f]+|[0-9]+)\)/g;
  let replaced = 0;

  const out = t.replace(callRe, (matched, name: string, literal: string) => {
    const decode = table.get(name);
    if (!decode) {
      return matched;
    }

    let value: unknown;
    try {
      value = decode(literal.startsWith("0x") ? Number.parseInt(literal, 16) : Number.parseInt(literal, 10));
    } catch {
      return matched;
    }

    // 解不出来（越界/未定义）就保持原样，避免误替换
    if (typeof value !== "string") {
      return matched;
    }

    replaced += 1;
    return JSON.stringify(value);
  });

  const dst = positional[1] ?? file.replace(/\.js$/, "") + ".deob.js";
  await Bun.write(dst, out);

  console.log(`源文件   : ${file} (${t.length})`);
  console.log(`解码器   : ${decoders.size} 个，含别名 ${table.size} 个`);
  console.log(`替换字符串: ${replaced} 处`);
  console.log(`输出     : ${dst} (${out.length})`);
}

await main();
