/* 通用 obfuscator.io 反混淆器：
 * 1) 识别字符串数组函数 + 解码函数（3种形态）
 * 2) 通用提取并在沙箱中执行所有数组旋转 IIFE（3种形态）
 * 3) 词法扫描（跳过注释/字符串/正则字面量），作用域感知地把 别名(数字) 替换为字符串
 */
const fs = require('fs');
const vm = require('vm');

function extractFunction(src, name) {
  const start = src.indexOf('function ' + name + '(');
  if (start < 0) return null;
  let i = src.indexOf('{', start), depth = 0;
  for (let j = i; j < src.length; j++) {
    const c = src[j];
    if (c === '{') depth++;
    else if (c === '}' && --depth === 0) return src.slice(start, j + 1);
  }
  return null;
}

function deobfuscate(file, outFile) {
  const src = fs.readFileSync(file, 'utf8');
  const n = src.length;

  // ---- 解码器识别 ----
  const decoders = new Map(); // decName -> arrName
  // 形态1: function D(a,b){const c=A();return(D=function(r,i){return c[r-=N]})(a,b)}
  let re = /function\s+([A-Za-z_$][\w$]*)\s*\(\s*[\w$]+\s*,\s*[\w$]*\s*\)\s*\{\s*const\s+[\w$]+\s*=\s*([A-Za-z_$][\w$]*)\s*\(\s*\)\s*;\s*return\s*\(\s*\1\s*=\s*function\s*\(\s*[\w$]+\s*,\s*[\w$]*\s*\)\s*\{\s*return\s+[\w$]+\s*\[\s*[\w$]+\s*-=\s*-?\d+\s*\]\s*\}\s*\)\s*\(/g;
  let mm;
  while ((mm = re.exec(src))) decoders.set(mm[1], mm[2]);
  // 形态2: function D(a,b){return a=a-N,A()[a]}
  re = /function\s+([A-Za-z_$][\w$]*)\s*\(\s*([\w$]+)\s*,\s*[\w$]*\s*\)\s*\{\s*return\s+\2\s*=\s*\2\s*-\s*\d+\s*,\s*([A-Za-z_$][\w$]*)\s*\(\s*\)\s*\[\s*\2\s*\]\s*\}/g;
  while ((mm = re.exec(src))) decoders.set(mm[1], mm[3]);
  // 形态2c: function D(a,b){return a-=N,A()[a]}
  re = /function\s+([A-Za-z_$][\w$]*)\s*\(\s*([\w$]+)\s*,\s*[\w$]*\s*\)\s*\{\s*return\s+\2\s*-=\s*(?:0x[0-9a-fA-F]+|\d+)\s*,\s*([A-Za-z_$][\w$]*)\s*\(\s*\)\s*\[\s*\2\s*\]\s*\}/g;
  while ((mm = re.exec(src))) decoders.set(mm[1], mm[3]);
  // 形态2b: function D(a,b){const c=A();return c[a-=N]}
  re = /function\s+([A-Za-z_$][\w$]*)\s*\(\s*[\w$]+\s*,\s*[\w$]*\s*\)\s*\{\s*const\s+[\w$]+\s*=\s*([A-Za-z_$][\w$]*)\s*\(\s*\)\s*;\s*return\s+[\w$]+\s*\[\s*[\w$]+\s*-\s*\d+\s*\]\s*\}/g;
  while ((mm = re.exec(src))) decoders.set(mm[1], mm[2]);
  // 形态3: RC4/base64 自修改解码器
  re = /function\s+([A-Za-z_$][\w$]*)\s*\(\s*([\w$]+)\s*,\s*[\w$]*\s*\)\s*\{\s*\2\s*=\s*\2\s*-\s*(?:0x[0-9a-fA-F]+|\d+)\s*;?\s*const\s+[\w$]+\s*=\s*([A-Za-z_$][\w$]*)\s*\(\s*\)\s*;/g;
  while ((mm = re.exec(src))) decoders.set(mm[1], mm[3]);

  const decNames = new Set(decoders.keys());
  const arrNames = new Set(decoders.values());
  console.log(file.split(/[\\/]/).pop(), 'decoders:', decNames.size, 'arrays:', arrNames.size);

  // ---- 通用提取旋转 IIFE ----
  // 结束形态: A }(ARR,NUM));  B })(ARR,NUM);  C })();  C2 逗号链 })(),
  const endRe = /\}(\s*\(\s*\w+\s*,\s*(?:0x[0-9a-fA-F]+|\d+)\s*\)\s*\)\s*;|\s*\)\s*\(\s*\w+\s*,\s*(?:0x[0-9a-fA-F]+|\d+)\s*\)\s*;|\s*\)\s*\(\s*\)\s*[;,])/g;
  const rotors = [];
  while ((mm = endRe.exec(src))) {
    const end = mm.index + mm[0].length;
    // 从 mm.index 的 '}' 反向花括号匹配函数体
    let depth = 1, j = mm.index;
    while (depth > 0 && j > 0) { j--; const c = src[j]; if (c === '}') depth++; else if (c === '{') depth--; }
    // j=函数体'{'；向前匹配参数表 )(
    let k = j - 1;
    while (/\s/.test(src[k] || '')) k--;
    if (src[k] !== ')') continue;
    let pd = 1;
    while (pd > 0 && k > 0) { k--; if (src[k] === ')') pd++; else if (src[k] === '(') pd--; }
    let p = k - 1;
    while (/\s/.test(src[p] || '')) p--;
    // 可选函数名
    const nm = /[A-Za-z_$][\w$]*/.exec(src.slice(p - 30, p + 1));
    if (nm && nm.index + nm[0].length === 31) p = Math.max(0, p - 30) + nm.index;
    // 跳过 'function'
    const fmM = /function\s*[A-Za-z_$][\w$]*$|function$/.exec(src.slice(Math.max(0, p - 20), p + 1));
    if (!fmM) continue;
    let q = Math.max(0, p - 20) + fmM.index;
    while (/\s/.test(src[q - 1] || '')) q--;
    if (src[q - 1] !== '(') continue;
    const text = src.slice(q - 1, end);
    if (/^\(function/.test(text)) rotors.push(text);
  }

  // ---- 沙箱装配 ----
  const sandbox = {}; vm.createContext(sandbox);
  const parts = [];
  for (const a of arrNames) { const f = extractFunction(src, a); if (f) parts.push(f); }
  for (const d of decNames) { const f = extractFunction(src, d); if (f) parts.push(f); }
  let rotorOk = 0;
  for (const r of rotors) {
    const expr = r.replace(/[,;]\s*$/, '');
    parts.push('try{0,' + expr + '}catch(e){}');
    rotorOk++;
  }
  parts.push(...[...decNames].map(d => 'this["' + d + '"]=' + d + ';'));
  vm.runInContext(parts.join('\n'), sandbox, { timeout: 120000 });
  console.log('rotors executed:', rotorOk);

  function tryDecode(name, num) {
    const fn = sandbox[name];
    if (typeof fn !== 'function') return undefined;
    try { const v = fn(num); return typeof v === 'string' ? v : undefined; } catch (e) { return undefined; }
  }

  // ---- O(n) 手动词法扫描 + 作用域替换 ----
  const scopes = [Object.create(null)];

  function resolve(name) {
    if (decNames.has(name)) return name;
    const seen = new Set();
    for (let s = scopes.length - 1; s >= 0; s--) {
      let cur = scopes[s][name];
      if (!cur) continue;
      while (cur && !seen.has(cur)) {
        seen.add(cur);
        if (decNames.has(cur)) return cur;
        let nxt = null;
        for (let t = s; t >= 0; t--) if (scopes[t][cur]) { nxt = scopes[t][cur]; break; }
        cur = nxt;
      }
    }
    return globalAlias[name] || null;
  }

  const isIdStart = c => /[A-Za-z_$]/.test(c);
  const isId = c => /[\w$]/.test(c);
  const isDigit = c => c >= '0' && c <= '9';
  const skipWs = p => { while (p < n && /\s/.test(src[p])) p++; return p; };

  // 全局别名映射（不动点迭代）：ID -> decoderName
  const globalAlias = Object.create(null);
  for (const d of decNames) globalAlias[d] = d;
  let aliasChanged = true;
  const aliasRe = /(?:(?:const|let|var)\s+|[,;{(]\s*)([A-Za-z_$][\w$]*)\s*=\s*([A-Za-z_$][\w$]*)\s*(?=[,;)\n}])/g;
  while (aliasChanged) {
    aliasChanged = false;
    let am;
    while ((am = aliasRe.exec(src))) {
      const [, lhs, rhs] = am;
      if (globalAlias[rhs] && !globalAlias[lhs]) { globalAlias[lhs] = globalAlias[rhs]; aliasChanged = true; }
    }
    aliasRe.lastIndex = 0;
  }

  // 预扫描：IIFE (function(a,b){...})(arg1,arg2) 的参数→解码器绑定
  // bodyStartIndex -> { param: decoderName }
  const iifeParams = new Map();
  for (let s = 0; s < n;) {
    const f = src.indexOf('function', s);
    if (f < 0) break;
    s = f + 8;
    // 边界检查
    if (f > 0 && isId(src[f - 1])) continue;
    let p = skipWs(f + 8);
    if (isIdStart(src[p] || '')) { while (p < n && isId(src[p])) p++; p = skipWs(p); }
    if (src[p] !== '(') continue;
    // 参数表
    const params = [];
    let pd = 1, q = p + 1, segStart = q;
    while (q < n && pd > 0) {
      const cq = src[q];
      if (cq === '(') pd++;
      else if (cq === ')') { pd--; if (pd === 0) break; }
      else if (cq === ',' && pd === 1) { params.push(src.slice(segStart, q)); segStart = q + 1; }
      q++;
    }
    const paramNames = params.map(x => x.trim()).filter(x => /^[A-Za-z_$][\w$]*$/.test(x));
    if (src[q] !== ')') continue;
    // 函数体
    const bodyStart = skipWs(q + 1);
    if (src[bodyStart] !== '{') continue;
    let bd = 1, z = bodyStart + 1;
    while (z < n && bd > 0) {
      const cz = src[z];
      if (cz === '"' || cz === "'" || cz === '`') {
        z++;
        while (z < n && src[z] !== cz) { if (src[z] === '\\') z++; z++; }
      } else if (cz === '{') bd++;
      else if (cz === '}') bd--;
      z++;
    }
    const bodyEnd = z - 1; // '}'
    // 调用形态 B: }(...)  形态 A: }(...))
    let c1 = skipWs(bodyEnd + 1);
    let style = 0, callStart = -1, callEnd = -1;
    if (src[c1] === '(') { style = 1; callStart = c1; }
    else if (src[c1] === ')') { const c2 = skipWs(c1 + 1); if (src[c2] === '(') { style = 2; callStart = c2; } }
    if (style) {
      let cd = 1, w = callStart + 1, as = w;
      const args = [];
      while (w < n && cd > 0) {
        const cw = src[w];
        if (cw === '"' || cw === "'" || cw === '`') {
          w++;
          while (w < n && src[w] !== cw) { if (src[w] === '\\') w++; w++; }
        } else if (cw === '(' || cw === '[') cd++;
        else if (cw === ')' || cw === ']') { cd--; if (cd === 0) { callEnd = w; break; } }
        else if (cw === ',' && cd === 1) { args.push(src.slice(as, w)); as = w + 1; }
        w++;
      }
      if (args.length) {
        const bindings = Object.create(null);
        let has = false;
        paramNames.forEach((pn, idx) => {
          const arg = (args[idx] || '').trim();
          if (/^[A-Za-z_$][\w$]*$/.test(arg) && globalAlias[arg]) { bindings[pn] = globalAlias[arg]; has = true; }
        });
        if (has) iifeParams.set(bodyStart, bindings);
      }
    }
  }
  console.log('iife param bindings:', iifeParams.size);

  let out = '', i = 0;
  let replaced = 0, failed = 0;
  const failedSamples = [];
  let prevSig = ''; // 上一个有意义的非空白字符

  function push(s) { out += s; }

  while (i < n) {
    const c = src[i];
    // 注释
    if (c === '/' && src[i + 1] === '/') {
      let j = i + 2; while (j < n && src[j] !== '\n') j++;
      push(src.slice(i, j)); i = j; continue;
    }
    if (c === '/' && src[i + 1] === '*') {
      let j = i + 2; while (j < n && !(src[j] === '*' && src[j + 1] === '/')) j++;
      j = Math.min(n, j + 2); push(src.slice(i, j)); i = j; continue;
    }
    // 字符串
    if (c === '"' || c === "'" || c === '`') {
      let j = i + 1;
      while (j < n) {
        if (src[j] === '\\') { j += 2; continue; }
        if (src[j] === c) { j++; break; }
        j++;
      }
      push(src.slice(i, j)); prevSig = c; i = j; continue;
    }
    // 正则字面量（启发式：/ 前一个有效字符是运算符/标点/关键字结尾）
    if (c === '/' && src[i + 1] !== '/' && src[i + 1] !== '*') {
      const canRegex = prevSig === '' || '(,=:[!&|?{};+-*%<>~^'.includes(prevSig);
      if (canRegex) {
        let j = i + 1, inClass = false, ok = false;
        while (j < n) {
          const cj = src[j];
          if (cj === '\\') { j += 2; continue; }
          if (cj === '[') inClass = true;
          else if (cj === ']') inClass = false;
          else if (cj === '/' && !inClass) { j++; ok = true; break; }
          else if (cj === '\n') break;
          j++;
        }
        if (ok) {
          while (j < n && /[gimsuy]/.test(src[j])) j++;
          push(src.slice(i, j)); prevSig = '/'; i = j; continue;
        }
      }
    }
    // 花括号作用域
    if (c === '{') {
      const sc = Object.create(null);
      const bindings = iifeParams.get(i);
      if (bindings) Object.assign(sc, bindings);
      scopes.push(sc);
      push(c); prevSig = c; i++; continue;
    }
    if (c === '}') { if (scopes.length > 1) scopes.pop(); push(c); prevSig = c; i++; continue; }

    // 标识符/关键字
    if (isIdStart(c)) {
      let j = i + 1;
      while (j < n && isId(src[j])) j++;
      const word = src.slice(i, j);
      const after = j;

      // 声明 const/let/var：仅用于记录别名，不吞文本（内部调用照常转换）
      if (word === 'const' || word === 'let' || word === 'var') {
        // 找到该语句的 ';'（同括号深度，跳过字符串）
        let k = after, pd = 0;
        while (k < n) {
          const ck = src[k];
          if (ck === '"' || ck === "'" || ck === '`') {
            k++;
            while (k < n && src[k] !== ck) { if (src[k] === '\\') k++; k++; }
            k++; continue;
          }
          if (ck === '(' || ck === '[') pd++;
          else if (ck === ')' || ck === ']') pd--;
          else if (ck === ';' && pd === 0) break;
          else if (ck === '{' && pd === 0) break;
          k++;
        }
        const stmtEnd = k;
        const stmt = src.slice(after, stmtEnd);
        for (const part of stmt.split(',')) {
          const dm = /^\s*([A-Za-z_$][\w$]*)\s*=\s*([A-Za-z_$][\w$]*)\s*$/.exec(part);
          if (dm) {
            const target0 = decNames.has(dm[2]) ? dm[2] : (resolve(dm[2]) || globalAlias[dm[2]]);
            if (target0) scopes[scopes.length - 1][dm[1]] = target0;
          }
        }
        push(word); prevSig = 't'; i = after; continue;
      }

      // 调用 word( 纯数字 )
      let k = after;
      while (k < n && (src[k] === ' ' || src[k] === '\t')) k++;
      if (src[k] === '(') {
        let p = k + 1;
        while (p < n && (src[p] === ' ' || src[p] === '\t' || src[p] === '\n')) p++;
        const numStart = p;
        if (src[p] === '0' && (src[p + 1] === 'x' || src[p + 1] === 'X')) {
          p += 2; while (p < n && /[0-9a-fA-F]/.test(src[p])) p++;
        } else {
          while (p < n && isDigit(src[p])) p++;
        }
        const numText = src.slice(numStart, p);
        let q = p;
        while (q < n && (src[q] === ' ' || src[q] === '\t' || src[q] === '\n')) q++;
        if (numText && src[q] === ')') {
          const target = resolve(word);
          if (target) {
            const num = numText.startsWith('0x') ? parseInt(numText, 16) : parseInt(numText, 10);
            const v = tryDecode(target, num);
            if (v !== undefined) {
              push(JSON.stringify(v)); replaced++;
              i = q + 1; prevSig = ')'; continue;
            }
            failed++;
            if (failedSamples.length < 10) failedSamples.push(target + '(' + num + ')');
          }
        }
      }

      push(word); prevSig = word[word.length - 1]; i = after;
      // 裸赋值 ID=ID（如 Vt=PV / Hc=yT，无声明关键字）
      if (!/[=!<>:]/.test(prevSig)) {
        let z = after;
        while (z < n && (src[z] === ' ' || src[z] === '\t')) z++;
        if (src[z] === '=' && src[z + 1] !== '=' && src[z - 1] !== '=' && src[z - 1] !== '!' && src[z - 1] !== '<' && src[z - 1] !== '>') {
          let w = z + 1;
          while (w < n && (src[w] === ' ' || src[w] === '\t')) w++;
          if (isIdStart(src[w] || '')) {
            let e2 = w + 1;
            while (e2 < n && isId(src[e2])) e2++;
            const rhs = src.slice(w, e2);
            const target0 = decNames.has(rhs) ? rhs : (resolve(rhs) || globalAlias[rhs]);
            if (target0) scopes[scopes.length - 1][word] = target0;
          }
        }
      }
      continue;
    }

    push(c);
    if (!/\s/.test(c)) prevSig = c;
    i++;
  }
  console.log('replaced:', replaced, 'failed:', failed, failedSamples.join(', '));
  fs.writeFileSync(outFile, out);
}

deobfuscate('d:/code/aicode/hidtest/web/assets/vendor-ROqIirej.js', 'd:/code/aicode/hidtest/vendor.deob.js');
deobfuscate('d:/code/aicode/hidtest/web/assets/index-ittJ_Bat.js', 'd:/code/aicode/hidtest/index.deob.js');
