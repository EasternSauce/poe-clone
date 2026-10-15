import fs from 'node:fs/promises';
import path from 'node:path';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

// Run from the project root after the Edit-mode survey. This report consumes
// calculated observations; it does not substitute spreadsheet formulas for combat.
const directory = path.resolve(process.argv[2] ?? 'BalanceResults');
const report = JSON.parse(await fs.readFile(path.join(directory, process.argv[3] ?? 'survey.json'), 'utf8'));
if (!report.model.startsWith('boss-single-target-') || report.targets?.length !== 102) {
  throw new Error('A fresh full boss survey with explicit target metadata is required.');
}
const archetypeOrder = new Map(report.archetypes.map((a, i) => [a.id, i]));
report.rows.sort((a, b) => archetypeOrder.get(a.archetype) - archetypeOrder.get(b.archetype) || a.level - b.level);
const wb = Workbook.create();
const overview = wb.worksheets.add('Skill overview');
const heatmap = wb.worksheets.add('Archetype comparison');
const dpsSheet = wb.worksheets.add('DPS by level');
const manaSheet = wb.worksheets.add('Mana consumption');
const survival = wb.worksheets.add('Survival');
const catalogue = wb.worksheets.add('Archetype catalogue');
const method = wb.worksheets.add('Method');
const targets = wb.worksheets.add('Boss targets');
const sheets = [overview, heatmap, dpsSheet, manaSheet, survival, catalogue, method, targets];
const mainArchetypes = report.archetypes.filter(a => a.family !== 'Support');
const byId = new Map(report.archetypes.map(a => [a.id, a]));
const rowKey = r => `${r.archetype}:${r.level}`;
const rows = report.rows.filter(r => r.attempted > 0);
const manaRows = report.rows.filter(r => r.attempted > 0 || r.family === 'Support');
const rowMap = new Map(report.rows.map(r => [rowKey(r), r]));
const skillNames = [...new Set(mainArchetypes.map(a => a.skill))];
const levelGroups = Array.from({ length: 34 }, (_, i) => report.rows.filter(r => r.level === i + 1 && r.topCount > 0));
const bestRows = skillNames.map(skill => levelGroups.map(group => group.filter(r => r.skill === skill).sort((a, b) => b.normalizedDps - a.normalizedDps)[0]));
const colors = { navy: '#28364A', red: '#F8D7DA', blue: '#DCEAF8', neutral: '#F1F3F5', amber: '#FFF0CA' };
const skillLabel = skill => skill.replace(/([a-z])([A-Z])/g, '$1 $2');

function column(index) {
  let out = '';
  for (let n = index + 1; n > 0; n = Math.floor((n - 1) / 26)) out = String.fromCharCode(65 + (n - 1) % 26) + out;
  return out;
}
function table(sheet, title, note, headers, values, name, widths = []) {
  sheet.showGridLines = false;
  sheet.getRange('A1').values = [[title]];
  sheet.getRange('A1').format.font = { name: 'Arial', size: 15, bold: true };
  sheet.getRange('A2').values = [[note]];
  sheet.getRange('A2').format.font = { name: 'Arial', size: 10, italic: true, color: '#596579' };
  const last = column(headers.length - 1), end = values.length + 4;
  sheet.getRange(`A4:${last}${end}`).values = [headers, ...values];
  sheet.getRange(`A4:${last}${end}`).format.font = { name: 'Arial', size: 10 };
  sheet.getRange(`A4:${last}${end}`).format.rowHeight = 21;
  sheet.getRange(`A4:${last}${end}`).format.verticalAlignment = 'center';
  sheet.getRange(`A4:${last}4`).format = { fill: colors.navy, font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' }, wrapText: true, horizontalAlignment: 'center', rowHeight: 42 };
  const t = sheet.tables.add(`A4:${last}${end}`, true, name);
  t.style = 'TableStyleLight1';
  t.showFilterButton = true;
  sheet.freezePanes.freezeRows(4);
  if (headers.length > 3) sheet.freezePanes.freezeColumns(2);
  for (let i = 0; i < headers.length; i++) sheet.getRange(`${column(i)}4:${column(i)}${end}`).format.columnWidth = widths[i] ?? 16;
  return { end, last };
}
function percentage(sheet, col, end) { sheet.getRange(`${col}5:${col}${end}`).setNumberFormat('0.0%'); }
function number(sheet, col, end, format = '#,##0.0') { sheet.getRange(`${col}5:${col}${end}`).setNumberFormat(format); }
function bands(sheet, range, low = .75, high = 1.25) {
  const r = sheet.getRange(range);
  r.conditionalFormats.add('cellIs', { operator: 'lessThan', formula: low, format: { fill: colors.blue, font: { color: '#154775' } } });
  r.conditionalFormats.add('cellIs', { operator: 'greaterThan', formula: high, format: { fill: colors.red, font: { color: '#8D2430' } } });
}
function levels(list) {
  if (!list.length) return 'None';
  const groups = [];
  let start = list[0], previous = start;
  for (const value of [...list.slice(1), Infinity]) {
    if (value !== previous + 1) { groups.push(start === previous ? `${start}` : `${start}–${previous}`); start = value; }
    previous = value;
  }
  return groups.join(', ');
}
function optional(value, predicate = false) { return predicate ? null : value; }

const overviewValues = skillNames.map((skill, i) => {
  const best = bestRows[i], measured = best.filter(Boolean);
  const family = mainArchetypes.find(a => a.skill === skill).family;
  const high = measured.filter(r => r.ratio > 1.25).map(r => r.level), low = measured.filter(r => r.ratio < .75).map(r => r.level);
  const hunger = measured.filter(r => r.representative.sustainableUseFraction < .75).map(r => r.level);
  return [skillLabel(skill), family, ...[1, 7, 14, 21, 28, 34].map(l => best[l - 1]?.ratio ?? null),
    levels(high), levels(low), levels(hunger), best[33]?.name ?? 'No viable build'];
});
overviewValues.sort((a, b) => (b[7] ?? -1) - (a[7] ?? -1));
let layout = table(overview, 'Boss single-target balance review', 'Best available archetype per skill. 100% = peer baseline after melee ×1.5 and minion ×1.3 targets. Red >125%; blue <75%.',
  ['Skill', 'Family', 'Level 1', 'Level 7', 'Level 14', 'Level 21', 'Level 28', 'Level 34', 'High DPS levels', 'Low DPS levels', 'Mana-limited levels', 'Best level 34 archetype'],
  overviewValues, 'SkillOverview', [22, 12, 12, 12, 12, 12, 12, 12, 23, 23, 25, 48]);
overview.getRange(`C5:H${layout.end}`).setNumberFormat('0%');
bands(overview, `C5:H${layout.end}`);
overview.getRange('A32').values = [['Mana-limited means regeneration covers less than 75% of full rotation demand. Starting mana can still sustain a short fight.']];
overview.getRange('A33').values = [['Recommendations are relative review flags. Read Survival first where very few sampled builds remain viable.']];
overview.getRange('A34').values = [[`Run: ${report.settings.samples} builds per available archetype/level; ${report.samples.length.toLocaleString('en-US')} builds; 20 rare rolls per slot; no uniques.`]];

const mapValues = mainArchetypes.map(a => [skillLabel(a.skill), a.name, a.role, ...Array.from({ length: 34 }, (_, i) => {
  const r = rowMap.get(`${a.id}:${i + 1}`); return r?.topCount > 0 ? r.ratio : null;
})]);
layout = table(heatmap, 'Archetypes across levels 1–34', 'Blank = unavailable weapon rank or no viable builds. Utility routes can trade single-target DPS for benefits outside this survey.',
  ['Skill', 'Archetype', 'Type', ...Array.from({ length: 34 }, (_, i) => `L${i + 1}`)], mapValues, 'ArchetypeComparison', [20, 55, 16, ...Array(34).fill(8)]);
heatmap.getRange(`D5:AK${layout.end}`).setNumberFormat('0%');
// Custom rules exclude blank cells from the low-DPS color.
heatmap.getRange(`D5:AK${layout.end}`).conditionalFormats.addCustom('AND(ISNUMBER(D5),D5<0.75)', { fill: colors.blue });
heatmap.getRange(`D5:AK${layout.end}`).conditionalFormats.addCustom('AND(ISNUMBER(D5),D5>1.25)', { fill: colors.red });
heatmap.freezePanes.freezeColumns(2);

const dpsValues = rows.map(r => [skillLabel(r.skill), r.name, r.family, r.level, r.topCount ? r.topDps : null,
  r.family === 'Melee' ? report.settings.meleePremium : r.family === 'Minion' ? report.settings.minionPremium : 1,
  null, r.baseline, null, r.status, r.viable, r.attempted, r.topCount, r.representative?.sample ?? null,
  r.representative?.rawDps ?? null, r.representative?.sustainedDps ?? null, r.archetype]);
layout = table(dpsSheet, 'DPS by archetype and level', 'Top DPS is the mean of the best 20% of viable builds over 30 seconds. Raw DPS assumes unlimited resources; cohort sample identifies a real representative build.',
  ['Skill', 'Archetype', 'Family', 'Level', 'Top 20% DPS', 'DPS target factor', 'Adjusted DPS', 'Peer baseline', 'Relative DPS', 'Review', 'Viable builds', 'Sampled builds', 'Top cohort size', 'Example sample', 'Example raw DPS', 'Example 30s DPS', 'Archetype ID'],
  dpsValues, 'DpsByLevel', [20, 55, 12, 9, 17, 17, 16, 16, 16, 45, 16, 17, 17, 17, 19, 19, 26]);
dpsSheet.getRange(`G5:G${layout.end}`).formulas = rows.map((r, i) => [r.topCount ? `=E${i + 5}/F${i + 5}` : '=""']);
dpsSheet.getRange(`I5:I${layout.end}`).formulas = rows.map((r, i) => [r.topCount && r.baseline > 0 ? `=G${i + 5}/H${i + 5}` : '=""']);
for (const c of ['E', 'G', 'H', 'O', 'P']) number(dpsSheet, c, layout.end);
percentage(dpsSheet, 'I', layout.end); bands(dpsSheet, `I5:I${layout.end}`);

const manaValues = manaRows.map(r => {
  const s = r.representative;
  return [skillLabel(r.skill), r.name, r.level, s?.sample >= 0 ? s.sample : null, s?.cost ?? null, s?.cost > 0 ? s.fullSpeedManaPerSecond / s.cost : s?.usesPerSecond ?? null,
    s?.fullSpeedManaPerSecond ?? null, s?.manaPerSecond ?? null, s?.supportManaPerSecond ?? null, null,
    s?.manaPool ?? null, s?.manaRegen ?? null, null, optional(s?.secondsToEmpty ?? null, s?.secondsToEmpty < 0),
    s?.sustainableUseFraction ?? null, s?.encounterUseFraction ?? null, s?.armyCost ?? null,
    s?.replacementManaPerSecond ?? null, s?.supports || 'Main skill only', r.status, r.archetype];
});
layout = table(manaSheet, 'Mana consumption', 'Main rows: an actual sampled build. Support rows: unequipped reference character. Summon setup is reserved before empty-time. Blank empty-time means no net drain.',
  ['Skill', 'Archetype', 'Level', 'Example sample', 'Mana / payment', 'Max payments / second', 'Spam mana / second', 'Rotation main mana / s', 'Support mana / second', 'Total mana / second',
    'Mana pool', 'Mana regen / second', 'Net drain / second', 'Seconds until empty', 'Infinite use fraction', '30s use fraction', 'Initial army mana', 'Full army replacement mana / s', 'Support rotation', 'Build review', 'Archetype ID'],
  manaValues, 'ManaConsumption', [20, 55, 9, 16, 15, 19, 21, 23, 22, 21, 16, 22, 21, 22, 20, 18, 20, 28, 44, 45, 26]);
manaSheet.getRange(`J5:J${layout.end}`).formulas = manaRows.map((r, i) => [r.representative ? `=H${i + 5}+I${i + 5}` : '=""']);
manaSheet.getRange(`M5:M${layout.end}`).formulas = manaRows.map((r, i) => [r.representative ? `=J${i + 5}-L${i + 5}` : '=""']);
for (const c of ['E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'Q', 'R']) number(manaSheet, c, layout.end);
percentage(manaSheet, 'O', layout.end); percentage(manaSheet, 'P', layout.end);
manaSheet.getRange(`O5:P${layout.end}`).conditionalFormats.add('cellIs', { operator: 'lessThan', formula: .75, format: { fill: colors.amber } });

const survivalRows = rows.filter(r => ['Melee', 'Minion'].includes(r.family));
const survivalValues = survivalRows.map(r => {
  const s = r.representative;
  return [skillLabel(r.skill), r.name, r.family, r.level, r.viableFraction, s?.life ?? null, s?.incomingHit ?? null, s?.incomingDps ?? null,
    s?.hitsToDie ?? null, optional(s?.secondsToDie ?? null, s?.secondsToDie >= 1e6), s?.lifeRecovery ?? null,
    s?.minionLife || null, optional(s?.minionSecondsToDie || null, s?.minionSecondsToDie < 0), r.family === 'Minion' ? s?.minionUptime ?? null : null, r.status, s?.bossEnraged ? 'Enraged' : 'Calm'];
});
layout = table(survival, 'Melee and minion survival', 'Equal-weight Mortis/Warlord/Rimeheart basic-attack pressure. Ranged/minions enrage; solo melee stays calm. Melee requires ≥4 unavoided hits and ≥6 seconds. Blank survival time means recovery covers incoming damage, or wolves are untargetable.',
  ['Skill', 'Archetype', 'Family', 'Level', 'Viable fraction', 'Player life', 'Enemy hit after defence', 'Incoming DPS', 'Hits to death', 'Seconds to death', 'Life recovery / second', 'Minion life', 'Minion seconds to death', 'Army uptime', 'Review', 'Boss state'],
  survivalValues, 'SurvivalResults', [20, 55, 12, 9, 19, 16, 26, 18, 19, 21, 25, 17, 26, 18, 45, 15]);
for (const c of ['F', 'G', 'H', 'I', 'J', 'K', 'L', 'M']) number(survival, c, layout.end);
percentage(survival, 'E', layout.end); percentage(survival, 'N', layout.end);
survival.getRange(`E5:E${layout.end}`).conditionalFormats.add('cellIs', { operator: 'lessThan', formula: .25, format: { fill: colors.red } });

const catalogueValues = report.archetypes.map(a => [skillLabel(a.skill), a.name, a.family, a.role, a.minimumWeaponPoints, a.mechanics || 'No required destination', a.requiredNodes.join(', '), a.passivePool.length, a.id]);
layout = table(catalogue, 'Playable skill routes', 'Every main-skill destination and compatible destination combination within ten points. Shared character passives vary within routes; six support skills are listed separately.',
  ['Skill', 'Archetype', 'Family', 'Route type', 'Required weapon points', 'Mechanics along route', 'Required weapon nodes', 'Relevant character passives', 'Archetype ID'],
  catalogueValues, 'ArchetypeCatalogue', [20, 55, 12, 18, 26, 70, 72, 29, 26]);
catalogue.getRange(`F5:G${layout.end}`).format.wrapText = true;
catalogue.getRange(`F5:G${layout.end}`).format.rowHeight = 47;

const targetValues = report.targets.map(t => [t.name, t.level, t.weight, t.life, t.armour,
  t.fireResistance, t.coldResistance, t.lightningResistance, t.poisonResistance, t.attackType,
  t.calmHit, t.enragedHit, t.calmAttacksPerSecond, t.enragedAttacksPerSecond, t.enragedDamageTaken]);
layout = table(targets, 'Boss reference targets', 'Each boss has one-third weight at every character level. These are scaled references, not campaign appearance levels. Basic attacks only; phases and special moves are excluded.',
  ['Boss', 'Level', 'Weight', 'Life', 'Armour', 'Fire resist %', 'Cold resist %', 'Lightning resist %', 'Poison resist %', 'Attack type', 'Calm raw hit', 'Enraged raw hit', 'Calm attacks / s', 'Enraged attacks / s', 'Enraged damage taken'],
  targetValues, 'BossReferenceTargets', [26, 9, 12, 16, 16, 18, 18, 22, 18, 16, 20, 20, 23, 24, 26]);
for (const c of ['D', 'E', 'F', 'G', 'H', 'I', 'K', 'L', 'M', 'N']) number(targets, c, layout.end);
percentage(targets, 'C', layout.end); percentage(targets, 'O', layout.end);

const methodValues = [
  ['Generated UTC', new Date(report.generatedUtc)], ['Model', report.model], ['Source revision before changes', report.sourceRevision],
  ['Random seed', report.settings.seed], ['Builds per available archetype/level', report.settings.samples], ['Rare rolls per equipment slot', report.settings.rareRollsPerSlot],
  ['Encounter seconds', report.settings.encounterSeconds], ['Melee DPS target factor', report.settings.meleePremium], ['Minion DPS target factor', report.settings.minionPremium],
  ['Relative review band', '75%–125%'], ['Mana measurements', 'One actual build nearest the top cohort mean. With no viable cohort, the strongest rejected build is shown. These are modelled values, not live combat measurements.'],
  ['Raw build records', 'sample-builds.csv contains every sampled build, equipment roll result, passive allocation, weapon allocation and exclusion reason.'],
  ...report.assumptions.map((text, i) => [`Assumption ${i + 1}`, text])
];
layout = table(method, 'Method and limits', 'Single-target approximations. Change simulation settings in code and rerun the survey; workbook tables are an analysis snapshot.',
  ['Setting or assumption', 'Value'], methodValues, 'SurveyMethod', [40, 120]);
method.getRange(`B5:B${layout.end}`).format.wrapText = true;
method.getRange(`A5:B${layout.end}`).format.rowHeight = 43;
method.getRange('B5').setNumberFormat('yyyy-mm-dd hh:mm:ss "UTC"');

// A source-linked trend chart provides a compact comparison without hundreds of
// archetype lines. Median of each skill's best archetype, by family and level.
const trends = Array.from({ length: 34 }, (_, i) => {
  const group = bestRows.map(best => best[i]).filter(Boolean);
  const median = family => {
    const values = group.filter(r => r.family === family).map(r => r.ratio).sort((a, b) => a - b);
    return values.length ? (values[Math.floor((values.length - 1) / 2)] + values[Math.floor(values.length / 2)]) / 2 : null;
  };
  return [`L${i + 1}`, median('Melee'), median('Minion'), median('Bow'), median('Spell')];
});
overview.getRange('N4:R38').values = [['Level', 'Melee', 'Minion', 'Bow', 'Spell'], ...trends];
overview.getRange('O5:R38').setNumberFormat('0%');
const chart = overview.charts.add('line', overview.getRange('N4:R38'));
chart.title = 'Median relative DPS of each family';
chart.titleTextStyle.typeface = 'Arial';
chart.titleTextStyle.fontSize = 12;
chart.setPosition('A37', 'L56');
chart.legend = { position: 'top', textStyle: { typeface: 'Arial', fontSize: 10 } };
chart.yAxis = { numberFormatCode: '0%', numberFormatSourceLinked: false, textStyle: { typeface: 'Arial', fontSize: 10 } };
chart.xAxis = { axisType: 'textAxis', textStyle: { typeface: 'Arial', fontSize: 10 } };
['#8F3D42', '#7254A3', '#4B7864', '#326895'].forEach((color, i) => chart.series.items[i].line = { fill: color, style: 'solid', width: 2 });

// Export all underlying observations as a flat, auditable companion file.
const fields = Object.keys(report.samples[0]);
const escape = value => `"${String(value ?? '').replaceAll('"', '""')}"`;
await fs.writeFile(path.join(directory, 'sample-builds.csv'), [fields.join(','), ...report.samples.map(s => fields.map(f => escape(s[f])).join(','))].join('\n'));
wb.recalculate();
console.log((await wb.inspect({ kind: 'table', range: 'Skill overview!A4:H9', include: 'values,formulas', tableMaxRows: 6, tableMaxCols: 8, maxChars: 2500 })).ndjson);
const errors = await wb.inspect({ kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!', options: { useRegex: true, maxResults: 10 }, maxChars: 1800 });
console.log(errors.ndjson);
if (errors.ndjson.split('\n').some(line => { try { return JSON.parse(line).kind === 'match'; } catch { return false; } })) throw new Error('Spreadsheet contains formula errors');
for (const index of [0, Math.floor(rows.length / 2), rows.length - 1]) {
  const row = rows[index];
  if (row.topCount && Math.abs(dpsSheet.getRange(`G${index + 5}`).values[0][0] - row.normalizedDps) > .001) throw new Error('Adjusted DPS does not reconcile');
}
for (const index of [0, Math.floor(manaRows.length / 2), manaRows.length - 1]) {
  const s = manaRows[index].representative;
  if (s && Math.abs(manaSheet.getRange(`J${index + 5}`).values[0][0] - s.manaPerSecond - s.supportManaPerSecond) > .001) throw new Error('Mana total does not reconcile');
}
if (!process.argv.includes('--no-previews')) {
  await fs.mkdir(path.join(directory, 'previews'), { recursive: true });
  for (const sheet of sheets) {
    const range = sheet === method ? 'A1:B10' : sheet === catalogue ? 'A1:F10' : sheet === heatmap ? 'A1:K12' : sheet === manaSheet ? 'A1:J10' : sheet === overview ? 'A1:H15' : 'A1:I10';
    const blob = await wb.render({ sheetName: sheet.name, range, scale: 1.5, format: 'png' });
    await fs.writeFile(path.join(directory, 'previews', `${sheet.name}.png`), new Uint8Array(await blob.arrayBuffer()));
  }
  for (const [sheetName, range, name] of [['Skill overview', 'A37:L56', 'Family trend'], ['Mana consumption', 'K4:R11', 'Mana sustain detail']]) {
    const blob = await wb.render({ sheetName, range, scale: 1.5, format: 'png' });
    await fs.writeFile(path.join(directory, 'previews', `${name}.png`), new Uint8Array(await blob.arrayBuffer()));
  }
}
const output = await SpreadsheetFile.exportXlsx(wb);
await output.save(path.join(directory, 'skill-balance.xlsx'));
console.log(JSON.stringify({ output: path.join(directory, 'skill-balance.xlsx'), archetypes: report.archetypes.length, samples: report.samples.length, measuredRows: rows.length }));
const finalists = bestRows.map((best, i) => ({ skill: skillNames[i], ratio: best[33]?.ratio ?? null, archetype: best[33]?.name ?? null,
  manaPerSecond: best[33]?.representative?.manaPerSecond ?? null, regen: best[33]?.representative?.manaRegen ?? null })).filter(r => r.ratio !== null).sort((a, b) => b.ratio - a.ratio);
console.log(JSON.stringify({ level34Highest: finalists.slice(0, 4), level34Lowest: finalists.slice(-4) }));
