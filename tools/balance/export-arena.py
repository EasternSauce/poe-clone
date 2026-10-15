#!/usr/bin/env python3
"""Export playable representative builds from an OfflineBalance rows report.

Usage: python3 tools/balance/export-arena.py BalanceResults/pass2-rows.json
The report's actual best-of-20 gear and allocations are preserved, not rerolled.
"""
import json
import pathlib
import sys

source = pathlib.Path(sys.argv[1])
report = json.loads(source.read_text())
builds = []
for row in report['rows']:
    sample = row.get('representative')
    if row['family'] == 'Support' or not row['attempted'] or not sample or not sample.get('gear'):
        continue
    builds.append(dict(id=row['archetype'], skill=row['skill'], name=row['name'], family=row['family'],
                       level=row['level'], sample=sample['sample'], weaponRank=sample['weaponRank'],
                       passives=sample['passives'], weaponNodes=sample['weaponNodes'], gear=sample['gear']))
if not builds:
    raise SystemExit('Report contains no playable builds')
target = pathlib.Path('Assets/Resources/BalanceArenaBuilds.json')
target.write_text(json.dumps(dict(source=source.name, generatedUtc=report['generatedUtc'], model=report['model'],
                                 rareRollsPerSlot=report['settings']['rareRollsPerSlot'], builds=builds), separators=(',', ':')) + '\n')
print(f'Exported {len(builds)} sampled builds to {target} ({target.stat().st_size} bytes)')
