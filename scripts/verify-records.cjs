// Run: node scripts/verify-records.cjs
// Minimal DOM adapter exercises the shared controls without a database or browser.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
class Element {
  constructor(tag, text = '') { this.tag = tag; this.text = text; this.children = []; this.dataset = {}; this.attrs = {}; this.events = {}; this.value = ''; }
  get textContent() { return this.text + this.children.map(c => c.textContent).join(''); }
  set textContent(value) { this.text = value; this.children = []; }
  append(...children) { children.forEach(c => { if (c.tag === 'fragment') this.append(...c.children); else this.children.push(c); }); }
  prepend(c) { this.children.unshift(c); }
  replaceChildren(...children) { this.children = []; this.text = ''; this.append(...children); }
  setAttribute(k, v) { this.attrs[k] = v; }
  hasAttribute(k) { return k in this.attrs; }
  addEventListener(name, handler) { this.events[name] = handler; }
  add(option) { if (!this.children.length) this.value = option.value; this.append(option); }
  querySelector(selector) { return this.find(c => selector === 'table' ? c.tag === 'table' : c.className === selector.slice(1)); }
  find(test) { for (const c of this.children) { if (test(c)) return c; const result = c.find(test); if (result) return result; } }
  fire(event) { this.events[event](); }
}
const root = new Element('section'), table = new Element('table'), body = new Element('tbody');
const headers = ['Stock','Quantity','Style','Date','Action'].map(t => new Element('th', t));
headers[1].dataset.type = 'number'; headers[2].dataset.filter = 'Style'; headers[3].dataset.type = 'date'; headers[4].attrs['data-no-sort'] = '';
const original = Array.from({length:127}, (_, i) => {
 const row = new Element('tr');
 row.cells = [new Element('td', `Stock ${i+1}`), new Element('td', String(i+1)), new Element('td', i % 2 ? 'Swing' : 'Intraday'), new Element('td', 'Date'), new Element('td','View')];
 row.cells[1].dataset.sort = String(i+1); row.cells[3].dataset.sort = i === 0 ? '' : `2026-09-${String(i+1).padStart(2,'0')}T12:00:00`;
 row.append(...row.cells); return row;
});
body.rows = original; body.append(...original); table.tBodies = [body]; table.tHead = {rows:[{cells:headers}]}; table.append(...headers, body); root.append(table);
vm.runInNewContext(fs.readFileSync('GetSetGo.Web/wwwroot/js/records.js','utf8'), {
 document: {querySelectorAll:()=>[root],createElement:t=>new Element(t),createDocumentFragment:()=>new Element('fragment')},
 Option: function(text,value) { const el = new Element('option',text); el.value=value; return el; },
 Intl, $: callback => callback()
});
const toolbar=root.querySelector('.record-toolbar'), pagination=root.querySelector('.record-pagination');
const search=toolbar.children[0].children[0], filter=toolbar.children[1].children[0], reset=toolbar.children[2];
const sizes=pagination.children[0].children[0], previous=pagination.children[1], next=pagination.children[3];
assert.equal(body.children.length,10); assert.equal(previous.disabled,true);
next.fire('click'); assert.equal(body.children[0],original[10]);
while (!next.disabled) next.fire('click'); assert.equal(body.children.length,7); assert.equal(next.disabled,true);
search.value='stock 27'; search.fire('input'); assert.deepEqual(body.children,[original[26]]);
search.value='missing'; search.fire('input'); assert.equal(body.children.length,0); assert.equal(root.querySelector('.record-empty').hidden,false);
reset.fire('click'); filter.value='Swing'; filter.fire('change'); assert.equal(body.children.length,10); assert.ok(body.children.every(r=>r.cells[2].textContent==='Swing'));
search.value='stock 2'; search.fire('input'); assert.deepEqual(body.children.map(r=>r.cells[0].textContent),['Stock 2','Stock 20','Stock 22','Stock 24','Stock 26','Stock 28']);
reset.fire('click'); headers[1].children[0].fire('click'); assert.equal(body.children[0],original[0]); headers[1].children[0].fire('click'); assert.equal(body.children[0],original[126]);
assert.equal(headers[1].attrs['aria-sort'],'descending');
headers[3].children[0].fire('click'); assert.equal(body.children[0],original[1]);
sizes.value='50'; sizes.fire('change'); assert.equal(body.children.length,50); next.fire('click'); next.fire('click'); assert.equal(body.children.length,27); assert.equal(body.children[26],original[0]);
reset.fire('click'); assert.equal(body.children.length,10); assert.equal(body.children[0],original[0]);
console.log('PASS: 127-row pagination, row count, combined search/filter, empty results, numeric/date sorting, null dates, reset, original row identity.');
