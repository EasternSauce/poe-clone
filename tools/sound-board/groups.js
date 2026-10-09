'use strict';
const path = require('node:path');

// Only explicit takes are grouped in the unassigned library. Numbered skills can be distinct effects.
function recordingFamily(asset) {
  const directory = path.posix.dirname(asset), ext = path.posix.extname(asset);
  let name = path.posix.basename(asset, ext);
  name = name.replace(/#\d+-\d{10,}/g, '#take');
  if (!name.includes('#take') && /^(?:swing|hurt|melee_hit|enemy_aggro|enemy_death|spider_hiss|wolf_growl|bite|slime|footstep|chain|lock|creak|doorOpen)[_ ]?\d+$/i.test(name))
    name = name.replace(/[_ ]?\d+$/, '_take');
  return directory + '/' + name;
}
function familyLabel(family) {
  return path.posix.basename(family).replace(/#take|_take/g, '').replace(/[_]+/g, ' ').replace(/\s+/g, ' ').trim();
}
function groupEntries(entries) {
  const groups = new Map();
  for (const entry of entries) {
    const used = entry.usages.length > 0;
    // For gameplay, the actual runtime assignment is authoritative, including mixed variation pools.
    const key = used ? entry.soundGroup || entry.id : 'unassigned:' + recordingFamily(entry.current[0]);
    let row = groups.get(key);
    if (!row) {
      row = { id: key, purpose: used ? entry.purpose || entry.label : 'Unassigned',
        label: used ? entry.purpose || entry.label : familyLabel(recordingFamily(entry.current[0])),
        group: entry.group, current: [], usages: [], labels: [], alternate: !!entry.soundGroup };
      groups.set(key, row);
    }
    row.labels.push(entry.label);
    row.current = [...new Set([...row.current, ...entry.current])];
    row.usages.push(...entry.usages.map(use => ({ ...use, recordings: entry.current })));
  }
  return [...groups.values()];
}
module.exports = { groupEntries, recordingFamily };
