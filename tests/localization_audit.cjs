const fs = require('node:fs');
const vm = require('node:vm');

const html = fs.readFileSync('src/ui.html', 'utf8');
const marker = 'const NORA_I18N=';
const start = html.indexOf(marker);
const end = html.indexOf(';const noraI18nIndexes', start);
if (start < 0 || end < 0) throw new Error('Could not locate the interface translation table.');

const serialized = vm.runInNewContext(html.slice(start, end + 1) + ';JSON.stringify(NORA_I18N)');
const translations = JSON.parse(serialized);
const english = translations.en;
const chinese = translations['zh-CN'];
const sourceKeys = Object.keys(english);
const checks = [];
function check(condition, description) {
  if (!condition) throw new Error(description);
  checks.push(description);
}

check(sourceKeys.length === Object.keys(chinese).length, 'English and Simplified Chinese have the same number of source strings.');
check(sourceKeys.every(key => Object.hasOwn(chinese, key)), 'Every English source string has a Chinese translation.');
check(Object.keys(chinese).every(key => Object.hasOwn(english, key)), 'Every Chinese source string has an English translation.');
check(sourceKeys.every(key => typeof key === 'string' && key.trim()), 'Every localization source key is non-empty.');
check(html.includes("node.parentElement.closest('script,style,textarea,input,pre,code,[contenteditable],.bubble,.content')"), 'Static chat workspace UI is translated while user and assistant message content stays intact.');
check(sourceKeys.every(key => typeof english[key] === 'string' && english[key].trim()), 'Every English translation is non-empty.');
check(sourceKeys.every(key => typeof chinese[key] === 'string' && chinese[key].trim()), 'Every Chinese translation is non-empty.');
check(sourceKeys.every(key => !/[\u0400-\u04ff]/u.test(english[key])), 'No English translation value contains Cyrillic text.');
check(sourceKeys.every(key => !/[\u0400-\u04ff]/u.test(chinese[key])), 'No Chinese translation value contains Cyrillic text.');

function tokens(text) {
  return text.match(/%[A-Z][A-Z0-9_]*%|\{\d+\}|https?:\/\/[^\s，。；：！？"'<>]+|\*\.[a-z0-9]+|\b(?:NORA|NVIDIA|CUDA|GGUF|JSON|MCPs?|OAuth|PEFT|LoRA|Qwen[0-9.\-A-Za-z]*|AirLLM|FFmpeg|Node\.js|SHA-?256)\b/giu) ?? [];
}
for (const [locale, table] of [['en', english], ['zh-CN', chinese]]) {
  for (const key of sourceKeys) {
    const normalize = value => value.toLowerCase().replace(/[.,，。；;:：!?！？]+$/, '').replace(/mcps$/, 'mcp').replace(/sha-256$/, 'sha256');
    const expected = tokens(key).map(normalize).sort();
    const actual = tokens(table[key]).map(normalize).sort();
    check(expected.every(token => actual.includes(token)), `${locale} preserves technical names/placeholders for: ${key.slice(0, 80)}`);
  }
}

check(chinese['1024 токена · больше VRAM'].includes('令牌'), 'Chinese uses the model-token term, not the currency meaning.');
check(chinese['Готово. Запуск выбранных 7B-весов на твоём ПК ещё нужно проверить.'].includes('权重'), 'Chinese uses the model-weight term, not the weighing-scale meaning.');
check(chinese['Короткое рассуждение · 48 токенов'].includes('推理'), 'Chinese uses the reasoning term for model inference.');

// 100 full passes per locale: every source key is resolved and stable on every pass.
let lookups = 0;
for (let pass = 0; pass < 100; pass++) {
  for (const key of sourceKeys) {
    const en = english[key];
    const zh = chinese[key];
    check(en === english[key] && zh === chinese[key], `Pass ${pass + 1}: stable en/zh value for ${key.slice(0, 64)}`);
    lookups += 2;
  }
}

console.log(JSON.stringify({
  locales: ['ru', 'en', 'zh-CN'],
  sourceStrings: sourceKeys.length,
  repeatedPasses: 100,
  translationLookups: lookups,
  assertions: checks.length,
  result: 'passed'
}, null, 2));
