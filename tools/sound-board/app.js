'use strict';
const $ = id => document.getElementById(id);
let rows = [], visible = [], limit = 80, activeAudio, activeButton, hideTimer, portraitAnchor, dismissPortraits = false;
function node(tag, text, className) {
  const element = document.createElement(tag);
  if (text !== undefined) element.textContent = text;
  if (className) element.className = className;
  return element;
}
function stop() {
  if (activeAudio) { activeAudio.onended = activeAudio.onerror = null; activeAudio.pause(); activeAudio.removeAttribute('src'); activeAudio.load(); }
  if (activeButton) { activeButton.textContent = 'Play'; activeButton.setAttribute('aria-pressed', 'false'); }
  activeAudio = activeButton = null;
}
function preview(clip, button) {
  if (activeButton === button) return stop();
  stop();
  const audio = new Audio('/audio?path=' + encodeURIComponent(clip.path));
  activeAudio = audio; activeButton = button; button.textContent = 'Stop'; button.setAttribute('aria-pressed', 'true');
  $('status').textContent = 'Preview: ' + clip.path.split('/').pop();
  audio.onended = () => { if (activeAudio === audio) { stop(); $('status').textContent = 'Preview finished.'; } };
  const failed = () => { if (activeAudio === audio) { stop(); $('status').textContent = 'This recording could not be played in your browser.'; } };
  audio.onerror = failed; audio.play().catch(failed);
}
function hidePortraits() { $('portraits').hidden = true; }
function queueHide() { clearTimeout(hideTimer); hideTimer = setTimeout(hidePortraits, 160); }
function portraits(enemies, anchor) {
  clearTimeout(hideTimer);
  if (dismissPortraits) return;
  if (!enemies.length) return hidePortraits();
  const panel = $('portraits'); $('portrait-grid').replaceChildren();
  for (const enemy of enemies) {
    const figure = node('figure'), img = node('img');
    img.src = '/portrait?path=' + encodeURIComponent(enemy.portrait); img.alt = enemy.name; img.width = img.height = 90;
    figure.append(img, node('figcaption', enemy.name)); $('portrait-grid').append(figure);
  }
  panel.hidden = false;
  portraitAnchor = anchor;
  positionPortraits();
}
function positionPortraits() {
  const panel = $('portraits'), anchor = portraitAnchor;
  if (panel.hidden || !anchor) return;
  const rect = anchor.getBoundingClientRect();
  if (rect.bottom < 0 || rect.top > innerHeight) return hidePortraits();
  panel.style.left = Math.max(12, Math.min(rect.right - panel.offsetWidth, innerWidth - panel.offsetWidth - 12)) + 'px';
  const below = innerHeight - rect.bottom - 12;
  panel.style.top = Math.max(12, below >= Math.min(panel.offsetHeight, 220) ? rect.bottom + 6 : rect.top - panel.offsetHeight - 6) + 'px';
}
function hover(target, enemies) {
  target.onmouseenter = () => portraits(enemies, target);
  target.onmouseleave = queueHide;
  target.onfocusin = event => { event.stopPropagation(); dismissPortraits = false; portraits(enemies, target); };
  target.onfocusout = queueHide;
  if (enemies.length) target.setAttribute('aria-describedby', 'portraits');
}
function render() {
  stop(); hidePortraits(); $('effects').replaceChildren();
  const query = $('search').value.trim().toLowerCase(), group = $('group').value, usage = $('usage').value;
  visible = rows.filter(row => (!group || group === row.group) &&
    (!usage || usage === 'used' && row.usages.length || usage === 'unused' && !row.usages.length || usage === 'enemy' && row.enemies.length) &&
    row.search.includes(query));
  $('count').textContent = `${visible.length} / ${rows.length} sound groups · ${new Set(rows.flatMap(row => row.clips.map(clip => clip.path))).size} recordings`;
  $('empty').hidden = visible.length > 0;
  let category;
  for (const row of visible.slice(0, limit)) {
    if (category !== row.group) { category = row.group; $('effects').append(node('h2', category, 'category')); }
    const card = node('section', undefined, 'effect'); card.tabIndex = 0; hover(card, row.enemies);
    const heading = node('h3', row.label); heading.append(node('span', row.clips.length + (row.clips.length === 1 ? ' recording' : ' recordings'), 'badge')); card.append(heading);
    if (row.enemies.length) card.append(node('p', `${row.enemies.length} ${row.enemies.length === 1 ? 'enemy uses' : 'enemies use'} these recordings · hover or focus to see them`, 'enemy-hint'));
    if (row.usages.length) {
      const list = node('ul', undefined, 'uses');
      for (const use of row.usages) {
        const item = node('li', use.label);
        if (use.muted || use.volume !== 1) item.append(node('span', use.muted ? 'Muted in game' : `Game gain ${Math.round(use.volume * 100)}%`, 'badge' + (use.muted ? ' muted' : '')));
        list.append(item);
      }
      card.append(list);
      const files = [...new Set(row.usages.flatMap(use => use.sources))].sort();
      if (files.length) {
        const details = node('details', undefined, 'sources'); details.append(node('summary', 'Usage references (' + files.length + ')'));
        for (const file of files) details.append(node('div', file)); card.append(details);
      }
    } else card.append(node('p', 'Not assigned to a current game effect. Available recording.'));
    const clips = node('div', undefined, 'clips');
    for (const clip of row.clips) {
      const recording = node('div', undefined, 'clip'), button = node('button', 'Play'), name = node('span', clip.path.split('/').pop().replaceAll('_', ' '));
      button.type = 'button'; button.setAttribute('aria-label', 'Preview ' + clip.path.split('/').pop()); button.setAttribute('aria-pressed', 'false');
      button.onclick = () => preview(clip, button); name.title = clip.path;
      hover(recording, clip.enemies); recording.append(button, name); clips.append(recording);
    }
    card.append(clips); $('effects').append(card);
  }
  $('more').hidden = visible.length <= limit;
  $('more').textContent = `Show more sounds (${visible.length - limit} remaining)`;
}
$('search').oninput = $('group').onchange = $('usage').onchange = () => { limit = 80; render(); };
$('more').onclick = () => { limit += 80; render(); };
$('stop').onclick = () => { stop(); $('status').textContent = 'Preview stopped.'; };
$('portraits').onmouseenter = () => clearTimeout(hideTimer); $('portraits').onmouseleave = queueHide;
window.addEventListener('keydown', event => { if (event.key === 'Escape') { dismissPortraits = true; hidePortraits(); stop(); } });
window.addEventListener('pointermove', event => { if (event.movementX || event.movementY) dismissPortraits = false; });
window.addEventListener('resize', hidePortraits);
window.addEventListener('scroll', event => { if (!$('portraits').contains(event.target)) positionPortraits(); }, true);
fetch('/api/board').then(response => { if (!response.ok) throw Error('Could not load sound board.'); return response.json(); }).then(data => {
  rows = data.effects.map(row => ({ ...row, search: JSON.stringify(row).toLowerCase() }));
  for (const group of [...new Set(rows.map(row => row.group))]) { const option = node('option', group); option.value = group; $('group').append(option); }
  render(); $('status').textContent = 'Read-only snapshot · ' + new Date(data.generatedAt).toLocaleString() + ' · No changes are saved.';
}).catch(error => { $('status').textContent = error.message; });
