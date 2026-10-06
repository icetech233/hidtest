const fs = require('fs');
const src = fs.readFileSync('d:/code/aicode/hidtest/web/assets/vendor-ROqIirej.js', 'utf8');
const WINDOW = 500;
for (const kw of ['requestDevice', 'sendFeatureReport', 'receiveFeatureReport']) {
  console.log('\n========== ' + kw + ' ==========');
  let idx = 0, n = 0;
  while (n < 8) {
    const f = src.indexOf(kw, idx);
    if (f < 0) break;
    n++;
    console.log(`[${n}] @${f}: ...${src.slice(Math.max(0, f - WINDOW), f + kw.length + 200).replace(/\s+/g, ' ')}...\n`);
    idx = f + kw.length;
  }
}
