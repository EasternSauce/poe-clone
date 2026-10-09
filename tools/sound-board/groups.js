'use strict';
function groupEntries(entries) {
  const groups = new Map();
  for (const entry of entries) {
    if (!entry.usages.length) continue;
    // For gameplay, the actual runtime assignment is authoritative, including mixed variation pools.
    const key = entry.soundGroup || entry.id;
    let row = groups.get(key);
    if (!row) {
      row = { id: key, purpose: entry.purpose || entry.label,
        label: entry.purpose || entry.label,
        group: entry.group, current: [], usages: [], labels: [], alternate: !!entry.soundGroup };
      groups.set(key, row);
    }
    row.labels.push(entry.label);
    row.current = [...new Set([...row.current, ...entry.current])];
    row.usages.push(...entry.usages.map(use => ({ ...use, recordings: entry.current })));
  }
  return [...groups.values()];
}
module.exports = { groupEntries };
