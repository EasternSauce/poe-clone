'use strict';
const $ = id => document.getElementById(id);
let sounds = [], token, page = 0, activeId = '', busy = false;
const pageSize = 60;
const formatBytes = bytes => bytes < 1024 * 1024 ? (bytes / 1024).toFixed(1) + ' KB' : (bytes / (1024 * 1024)).toFixed(1) + ' MB';
function filtered() {
  const query = $('search').value.trim().toLowerCase();
  return sounds.filter(row => (!query || [row.name, row.id, ...row.packs, ...row.sources.map(source => source.file)].some(text => text.toLowerCase().includes(query))) && (!$('category').value || row.category === $('category').value) && (!$('pack').value || row.packs.includes($('pack').value)));
}
function message(text, error = false) { $('status').textContent = text; $('status').classList.toggle('error', error); }
function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}
function clearPlayer() {
  activeId = ''; const audio = $('audio'); audio.pause(); audio.removeAttribute('src'); audio.load();
  $('now-playing').textContent = 'Choose a sound to preview';
}
async function mutate(endpoint, row, name) {
  if (busy) return;
  busy = true;
  // Release Windows file handles before moving or deleting the playing clip.
  if (activeId === row.id) clearPlayer();
  for (const control of document.querySelectorAll('.row button, .row input')) control.disabled = true;
  message(endpoint === '/api/delete' ? 'Deleting file...' : 'Renaming file...');
  try {
    const response = await fetch(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Library-Token': token }, body: JSON.stringify({ id: row.id, name }) });
    const result = await response.json();
    if (!response.ok) throw Error(result.error || 'File operation failed.');
    sounds = result.sounds; message(result.message);
  } catch (error) { message(error.message, true); }
  finally { busy = false; render(); }
}
function render() {
  $('total').textContent = sounds.length.toLocaleString();
  $('size').textContent = formatBytes(sounds.reduce((total, row) => total + row.bytes, 0));
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
    const item = element('div', 'row' + (row.id === activeId ? ' playing' : ''));
    const play = element('button', '', row.id === activeId && !$('audio').paused ? 'Pause' : 'Play');
    play.disabled = busy; play.setAttribute('aria-label', 'Preview ' + row.name);
    play.onclick = async () => {
      const audio = $('audio');
      if (activeId === row.id && !audio.paused) { audio.pause(); return; }
      if (activeId !== row.id) { activeId = row.id; audio.src = '/audio?id=' + encodeURIComponent(row.id); }
      $('now-playing').textContent = row.name + row.ext;
      try { await audio.play(); } catch { message('This sound could not be played. Check the file or browser audio support.', true); }
      updatePlayButtons();
    };
    item.dataset.id = row.id;
    const detail = element('div');
    const name = document.createElement('input'); name.type = 'text'; name.value = row.name; name.disabled = busy; name.maxLength = 100; name.setAttribute('aria-label', 'Filename for ' + row.id); name.title = 'Filename without extension; press Rename or Enter to apply';
    name.onkeydown = event => { if (event.key === 'Enter') { event.preventDefault(); mutate('/api/rename', row, name.value); } };
    detail.append(name, element('div', 'source', 'Assets/Audio/' + row.id));
    const pack = element('div', 'pack-info'); pack.append(element('div', 'pack', row.packs.join(', ')), element('div', 'ext', row.ext.slice(1).toUpperCase() + ' · ' + formatBytes(row.bytes)));
    const actions = element('div', 'actions');
    const rename = element('button', 'rename', 'Rename'); rename.disabled = busy; rename.onclick = () => mutate('/api/rename', row, name.value);
    const remove = element('button', 'delete', 'Delete'); remove.disabled = busy; remove.setAttribute('aria-label', 'Delete ' + row.name + row.ext); remove.onclick = () => mutate('/api/delete', row);
    actions.append(rename, remove);
    item.append(play, detail, pack, actions); list.append(item);
  }
  if (!matches.length) list.append(element('div', 'empty', sounds.length ? 'No sounds match these filters.' : 'No audio files found in Assets/Audio.'));
  $('page').textContent = matches.length ? `Page ${page + 1} of ${Math.ceil(matches.length / pageSize)} · ${matches.length.toLocaleString()} matches` : '0 matches';
  $('previous').disabled = page === 0 || busy; $('next').disabled = (page + 1) * pageSize >= matches.length || busy;
}
function updatePlayButtons() {
  for (const item of document.querySelectorAll('.row')) {
    item.classList.toggle('playing', item.dataset.id === activeId);
    item.querySelector('button').textContent = item.dataset.id === activeId && !$('audio').paused ? 'Pause' : 'Play';
  }
}
for (const id of ['search', 'category', 'pack']) $(id).addEventListener('input', () => { page = 0; render(); });
$('previous').onclick = () => { page--; render(); };
$('next').onclick = () => { page++; render(); };
for (const event of ['play', 'pause', 'ended']) $('audio').addEventListener(event, updatePlayButtons);
$('audio').addEventListener('error', () => { if (activeId) message('Unable to play this file. Refresh the library if it was moved or deleted elsewhere.', true); });
async function load() {
  if (busy) return;
  try {
    const response = await fetch('/api/library');
    if (!response.ok) throw Error('Could not load the sound library.');
    const data = await response.json();
    if (data.version !== 3) throw Error('Restart Sound Library to load the updated file browser.');
    sounds = data.sounds; token = data.token;
    if (activeId && !sounds.some(row => row.id === activeId)) clearPlayer();
    for (const [id, values, title] of [['category', sounds.map(row => row.category), 'All categories'], ['pack', sounds.flatMap(row => row.packs), 'All packs']]) {
      const selected = $(id).value; $(id).replaceChildren(element('option', '', title)); $(id).firstChild.value = '';
      for (const value of [...new Set(values)].sort()) { const option = element('option', '', value); option.value = value; $(id).append(option); }
      if ([...$(id).options].some(option => option.value === selected)) $(id).value = selected;
    }
    render();
  } catch (error) { message(error.message, true); }
}
$('refresh').onclick = load;
load();
