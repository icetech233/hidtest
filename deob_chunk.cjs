// 通用 javascript-obfuscator 反混淆：解出字符串数组并替换 X(0xNN) 调用
// 用法: node deob_chunk.cjs <js文件路径> [输出文件路径]
const fs = require('fs');
const file = process.argv[2];
if (!file) { console.log('usage: node deob_chunk.cjs <file> [out]'); process.exit(1); }
const t = fs.readFileSync(file, 'utf8');

function extractFn(src, start) {
  let i = src.indexOf('{', start), d = 1, q = 0, esc = false;
  for (i = i + 1; i < src.length && d > 0; i++) {
    const c = src.charCodeAt(i);
    if (q) { if (esc) esc = false; else if (c === 92) esc = true; else if (c === q) q = 0; continue; }
    if (c === 39 || c === 34 || c === 96) { q = c; continue; }
    if (c === 123) d++; else if (c === 125) d--;
  }
  return src.slice(start, i);
}

// 1) 字符串数组持有者 function HOLDER(){var ARR=[...]; HOLDER=function(){return ARR;}; return HOLDER();}
const hm = t.match(/function (_0x[0-9a-f]+)\(\)\{(?:var|const|let) (_0x[0-9a-f]+)=\[/);
if (!hm) { console.log('no string array found'); process.exit(1); }
const holderName = hm[1];
const holder = extractFn(t, t.indexOf('function ' + holderName + '(){'));

// 2) 解码器 function DEC(a,b){ a=a-0xNN; var x=HOLDER(); ... }
let decName = null, decSrc = '';
for (const m of t.matchAll(/function (_0x[0-9a-f]+)\(_0x[0-9a-f]+,_0x[0-9a-f]+\)\{/g)) {
  const body = extractFn(t, m.index);
  if (body.indexOf(holderName + '()') >= 0) { decName = m[1]; decSrc = body; break; }
}
if (!decName) { console.log('no decoder found'); process.exit(1); }

// 3) 数组旋转 IIFE (function(x,y){...}(HOLDER,0xNNNN));
let iife = '';
const mi = t.indexOf('(' + holderName + ',0x');
if (mi >= 0) {
  const s = t.lastIndexOf('(function', mi);
  const e = t.indexOf('));', mi);
  if (s >= 0 && e > mi) iife = t.slice(s, e + 3);
}

const f = new Function(holder + '\n' + decSrc + '\n' + iife + '; return ' + decName + ';');
const dec = f();

// 4) 收集解码器别名
let names = new Set([decName]);
for (let k = 0; k < 8; k++) {
  for (const m of t.matchAll(/(?:var|const|let) (_0x[0-9a-f]+)=(_0x[0-9a-f]+);/g)) {
    if (names.has(m[2])) names.add(m[1]);
  }
}

const out = t.replace(/(_0x[0-9a-f]+)\(0x([0-9a-f]+)\)/g,
  (mm, a, b) => names.has(a) ? JSON.stringify(dec(parseInt(b, 16))) : mm);

const dst = process.argv[3] || file.replace(/\.js$/, '') + '.deob.js';
fs.writeFileSync(dst, out);
console.log('holder=' + holderName, 'dec=' + decName, 'aliases=' + names.size, 'iife=' + iife.length);
console.log('written ' + dst + ' (' + out.length + ')');
