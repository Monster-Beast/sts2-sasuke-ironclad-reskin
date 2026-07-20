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

def main():
    for p in ['README.md','LEGAL.md','SasukeIronclad.json','SasukeIronclad.csproj','project.godot','SasukeIronclad/data/card_visual_map.json','SasukeIronclad/data/action_profiles.json']:
        if not (ROOT/p).exists(): die(f"missing {p}")
    manifest=load('SasukeIronclad.json')
    if manifest.get('id')!='SasukeIronclad': die('manifest id changed')
    if manifest.get('author')!='Monster-Beast': die('manifest author must be Monster-Beast')
    if manifest.get('affects_gameplay') is not False: die('affects_gameplay must be false')
    cards=load('SasukeIronclad/data/card_visual_map.json')
    actions=load('SasukeIronclad/data/action_profiles.json')
    if cards.get('gameplay_changes') is not False: die('card map declares gameplay changes')
    if actions.get('gameplay_timing_locked') is not True: die('action timing must remain locked')
    ids={a['id'] for a in actions['profiles']}
    if len(ids)!=len(actions['profiles']): die('duplicate action profile')
    seen=set()
    for c in cards['cards']:
        if c['card_id'] in seen: die(f"duplicate card {c['card_id']}")
        seen.add(c['card_id'])
        if c['action_profile'] not in ids: die(f"unknown action for {c['card_id']}")
        if c['legal_status']!='original_required': die(f"invalid legal status for {c['card_id']}")
    for a in actions['profiles']:
        if a['fallback']!='original': die(f"invalid fallback for {a['id']}")
        if not 200 <= int(a['max_duration_ms']) <= 2000: die(f"invalid duration for {a['id']}")
    for p in ['SasukeIronclad.csproj','Directory.Build.props','Sts2PathDiscovery.props']:
        try: ET.parse(ROOT/p)
        except Exception as exc: die(f"invalid XML {p}: {exc}")
    forbidden={'.pck','.dll','.atlas','.skel','.ogg','.mp3','.ttf','.otf'}
    bad=[str(p.relative_to(ROOT)) for p in ROOT.rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix.lower() in forbidden]
    if bad: die('forbidden binary/extracted assets: '+', '.join(bad))
    print(f"OK: {len(seen)} card concepts, {len(ids)} action profiles.")
    return 0

if __name__=='__main__': sys.exit(main())
