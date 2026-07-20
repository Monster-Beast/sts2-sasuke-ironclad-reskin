#!/usr/bin/env python3
from __future__ import annotations
import json, sys, xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def die(msg:str):
    print(f"ERROR: {msg}")
    raise SystemExit(1)

def load(path:str):
    try: return json.loads((ROOT/path).read_text(encoding='utf-8'))
    except Exception as exc: die(f"invalid JSON {path}: {exc}")

def unique(items, key, label):
    values=[item[key] for item in items]
    if len(values)!=len(set(values)): die(f"duplicate {label}")
    return set(values)

def main():
    required=[
        'README.md','LEGAL.md','SasukeIronclad.json','SasukeIronclad.csproj','project.godot',
        'SasukeIronclad/data/card_visual_map.json','SasukeIronclad/data/action_profiles.json',
        'SasukeIronclad/data/presentation_tiers.json','SasukeIronclad/data/presentation_surfaces.json',
        'docs/design/presentation-matrix.md','docs/research/reference-chizuru-ironclad.md'
    ]
    for p in required:
        if not (ROOT/p).exists(): die(f"missing {p}")
    manifest=load('SasukeIronclad.json')
    if manifest.get('id')!='SasukeIronclad': die('manifest id changed')
    if manifest.get('author')!='Monster-Beast': die('manifest author must be Monster-Beast')
    if manifest.get('affects_gameplay') is not False: die('affects_gameplay must be false')

    cards=load('SasukeIronclad/data/card_visual_map.json')
    actions=load('SasukeIronclad/data/action_profiles.json')
    tiers=load('SasukeIronclad/data/presentation_tiers.json')
    surfaces=load('SasukeIronclad/data/presentation_surfaces.json')
    if cards.get('schema_version')!=1: die('unsupported card schema')
    if actions.get('schema_version')!=2: die('unsupported action schema')
    if tiers.get('schema_version')!=1: die('unsupported tier schema')
    if surfaces.get('schema_version')!=1: die('unsupported surface schema')
    if cards.get('gameplay_changes') is not False or tiers.get('gameplay_changes') is not False:
        die('visual configuration declares gameplay changes')
    if actions.get('gameplay_timing_locked') is not True: die('action timing must remain locked')

    action_ids=unique(actions['profiles'],'id','action profile')
    for action in actions['profiles']:
        if action['fallback']!='original': die(f"invalid fallback for {action['id']}")
        if not 200 <= int(action['max_duration_ms']) <= 2000: die(f"invalid duration for {action['id']}")
        if not 0 <= float(action['impact_fraction']) <= 1: die(f"invalid impact fraction for {action['id']}")

    tier_ids=unique(tiers['tiers'],'id','presentation tier')
    ranks=[tier['rank'] for tier in tiers['tiers']]
    if len(ranks)!=len(set(ranks)): die('duplicate presentation tier rank')
    policy=tiers['default_policy']
    if policy.get('basic_common_cap') not in tier_ids or policy.get('fast_mode_cap') not in tier_ids:
        die('presentation policy has unknown tier cap')
    if policy.get('low_flash_available') is not True: die('low-flash mode is required')
    allowed_inputs={'attack_topology','final_damage','card_rarity','energy_spent','lethal','explicit_visual_tag'}
    if set(policy.get('runtime_inputs_read_only',[])) - allowed_inputs: die('unknown runtime presentation input')
    for tier in tiers['tiers']:
        if not set(tier['default_profiles']) <= action_ids:
            die(f"tier {tier['id']} references unknown action")
        if tier['id']=='finisher' and not tier.get('screen_takeover'): die('finisher must be marked as screen takeover')

    surface_ids=unique(surfaces['surfaces'],'id','presentation surface')
    if len(surface_ids)<20: die('presentation surface scope is not rich enough')
    for surface in surfaces['surfaces']:
        if surface.get('required') and not surface.get('fallback'):
            die(f"required surface lacks fallback: {surface['id']}")

    seen=set()
    for card in cards['cards']:
        if card['card_id'] in seen: die(f"duplicate card {card['card_id']}")
        seen.add(card['card_id'])
        if card['action_profile'] not in action_ids: die(f"unknown action for {card['card_id']}")
        if card['legal_status']!='original_required': die(f"invalid legal status for {card['card_id']}")

    for p in ['SasukeIronclad.csproj','Directory.Build.props','Sts2PathDiscovery.props']:
        try: ET.parse(ROOT/p)
        except Exception as exc: die(f"invalid XML {p}: {exc}")
    forbidden={'.pck','.dll','.atlas','.skel','.ogg','.mp3','.ttf','.otf'}
    bad=[str(p.relative_to(ROOT)) for p in ROOT.rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix.lower() in forbidden]
    if bad: die('forbidden binary/extracted assets: '+', '.join(bad))
    print(f"OK: {len(seen)} card concepts, {len(action_ids)} action profiles, {len(tier_ids)} tiers, {len(surface_ids)} surfaces.")
    return 0

if __name__=='__main__': sys.exit(main())
