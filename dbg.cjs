const fs = require('fs');
const s = fs.readFileSync('d:/code/aicode/hidtest/web/assets/index.deob2.js', 'utf8');
const positions = [];
let i = -1;
while ((i = s.indexOf('lightEffectHfd', i + 1)) >= 0) positions.push(i);
console.log('occurrences:', positions.length);
for (const p of positions) {
  console.log('=== @', p, '===');
  console.log(s.slice(p, p + 700));
  console.log();
}
