// Extracts every ```mermaid block from the wiki markdown sources (content/),
// mirroring what Zola passes to the mermaid renderer at page load.

'use strict';

const fs = require('fs');
const path = require('path');

function collectBlocks(contentRoot) {
  const out = [];
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(p);
      } else if (entry.name.endsWith('.md')) {
        const text = fs.readFileSync(p, 'utf8');
        const re = /```mermaid\n([\s\S]*?)```/g;
        let m, i = 0;
        while ((m = re.exec(text)) !== null) {
          out.push({ file: path.relative(contentRoot, p), n: i++, src: m[1] });
        }
      }
    }
  };
  walk(contentRoot);
  return out;
}

module.exports = { collectBlocks };
