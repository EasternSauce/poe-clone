'use strict';
const $ = id => document.getElementById(id);
let sounds = [], token, page = 0, activeId = '', saveTimer, revision = 0, savedRevision = 0, saveQueue = Promise.resolve(), exporting = false;
const pageSize = 60;
const formatBytes = bytes => bytes < 1024 * 1024 ? (bytes / 1024).toFixed(1) + ' KB' : (bytes / (1024 * 1024)).toFixed(1) + ' MB';
function filtered() {
  const query = $('search').value.trim().toLowerCase();
  return sounds.filter(row => (!query || [row.name, row.id, row.pack, row.category].some(text => text.toLowerCase().includes(query))) && (!$('category').value || row.category === $('category').value) && (!$('pack').value || row.pack === $('pack').value) && (!$('selection').value || row.selected === ($('selection').value === 'selected')));
}
function message(text, error = false) { $('status').textContent = text; $('status').classList.toggle('error', error); }
function updateStats() {
  const selected = sounds.filter(row => row.selected);
  $('total').textContent = sounds.length.toLocaleString();
  $('selected').textContent = selected.length.toLocaleString();
  $('size').textContent = formatBytes(selected.reduce((total, row) => total + row.bytes, 0));
  $('export').disabled = !selected.length || exporting;
}
function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}
function render() {
  updateStats();
  const matches = filtered();
  page = Math.max(0, Math.min(page, Math.ceil(matches.length / pageSize) - 1));
  const list = $('list'); list.replaceChildren();
  let lastCategory = '';
  for (const row of matches.slice(page * pageSize, (page + 1) * pageSize)) {
    if (lastCategory !== row.category) {
      const group = element('div', 'group');
      group.append(element('h2', '', row.category), element('span', '', matches.filter(item => item.category === row.category).length + ' matching sounds'));
      list.append(group); lastCategory = row.category;
    }
    const item = element('div', 'row' + (row.selected ? '' : ' excluded') + (row.id === activeId ? ' playing' : ''));
    const check = document.createElement('input'); check.type = 'checkbox'; check.checked = row.selected; check.disabled = exporting; check.setAttribute('aria-label', 'Keep ' + row.name);
    check.onchange = () => { row.selected = check.checked; changed(); render(); };
    const play = element('button', '', row.id === activeId && !$('audio').paused ? 'Pause' : 'Play');
    play.setAttribute('aria-label', 'Preview ' + row.name);
    play.onclick = async () => {
      const audio = $('audio');
      if (activeId === row.id && !audio.paused) { audio.pause(); return; }
      if (activeId !== row.id) { activeId = row.id; audio.src = '/audio?id=' + encodeURIComponent(row.id); }
      $('now-playing').textContent = row.name + row.ext;
      try { await audio.play(); } catch { message('This sound could not be played. Check the source file or browser audio support.', true); }
      render();
    };
    const detail = element('div');
    const name = document.createElement('input'); name.type = 'text'; name.value = row.name; name.disabled = exporting; name.maxLength = 100; name.setAttribute('aria-label', 'Export name for ' + row.id); name.title = 'Export filename, without extension';
    name.oninput = () => { row.name = name.value; changed(); };
    name.onchange = () => { if (activeId === row.id) $('now-playing').textContent = row.name + row.ext; };
    detail.append(name, element('div', 'source', row.id));
    const pack = element('div', 'pack-info'); pack.append(element('div', 'pack', row.pack), element('div', 'ext', row.ext.slice(1).toUpperCase() + ' · ' + formatBytes(row.bytes)));
    item.append(check, play, detail, pack); list.append(item);
  }
  if (!matches.length) list.append(element('div', 'empty', sounds.length ? 'No sounds match these filters.' : 'No audio files found in Assets/assets_for_inspiration.'));
  $('page').textContent = matches.length ? `Page ${page + 1} of ${Math.ceil(matches.length / pageSize)} · ${matches.length.toLocaleString()} matches` : '0 matches';
  $('previous').disabled = page === 0; $('next').disabled = (page + 1) * pageSize >= matches.length;
  $('select-visible').disabled = $('exclude-visible').disabled = !matches.length || exporting;
}
async function post(endpoint, body) {
  const response = await fetch(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Library-Token': token }, body: JSON.stringify(body) });
  const value = await response.json();
  if (!response.ok) throw Error(value.error || 'Request failed.');
  return value;
}
function snapshot() { return { sounds: sounds.map(({ id, name, selected }) => ({ id, name, selected })) }; }
function save() {
  clearTimeout(saveTimer);
  const body = snapshot(), currentRevision = revision;
  const operation = saveQueue.catch(() => {}).then(async () => {
    await post('/api/save', body);
    savedRevision = currentRevision;
    if (revision === currentRevision) $('save-status').textContent = 'All changes saved';
  });
  saveQueue = operation;
  operation.catch(error => { $('save-status').textContent = 'Changes not saved'; message(error.message, true); });
  return operation;
}
function changed() { revision++; $('save-status').textContent = 'Saving…'; message(''); clearTimeout(saveTimer); saveTimer = setTimeout(save, 450); }
for (const id of ['search', 'category', 'pack', 'selection']) $(id).addEventListener('input', () => { page = 0; render(); });
$('previous').onclick = () => { page--; render(); };
$('next').onclick = () => { page++; render(); };
for (const [id, selected] of [['select-visible', true], ['exclude-visible', false]]) $(id).onclick = () => { for (const row of filtered()) row.selected = selected; changed(); render(); };
$('audio').addEventListener('play', render); $('audio').addEventListener('pause', render); $('audio').addEventListener('ended', render);
$('audio').addEventListener('error', () => message('Unable to play this file. The original may have moved, or the format may be unsupported by your browser.', true));
window.addEventListener('beforeunload', event => { if (revision !== savedRevision) { event.preventDefault(); event.returnValue = ''; } });
$('export').onclick = async () => {
  exporting = true; $('export').textContent = 'Exporting…'; render(); message('Copying selected audio files…');
  // Freeze edits while taking and exporting a complete snapshot.
  for (const input of document.querySelectorAll('input, select')) input.disabled = true;
  try {
    await save();
    const result = await post('/api/export', snapshot());
    message(`Exported ${result.count.toLocaleString()} sounds to ${result.directory}. These copies can be kept after the inspiration folder is removed.`);
  } catch (error) { message(error.message, true); }
  finally {
    exporting = false; $('export').textContent = 'Export selected sounds →';
    for (const input of document.querySelectorAll('input, select')) input.disabled = false;
    render();
  }
};
(async () => {
  try {
    const response = await fetch('/api/library');
    if (!response.ok) throw Error('Could not load the sound library.');
    const data = await response.json(); sounds = data.sounds; token = data.token;
    for (const [id, field] of [['category', 'category'], ['pack', 'pack']]) for (const value of [...new Set(sounds.map(row => row[field]))].sort()) {
      const option = element('option', '', value); option.value = value; $(id).append(option);
    }
    $('save-status').textContent = 'All changes saved'; render();
  } catch (error) { $('save-status').textContent = 'Library unavailable'; message(error.message, true); }
})();
