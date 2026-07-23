#!/usr/bin/env python3
from __future__ import annotations
import configparser, json, re, sys, xml.etree.ElementTree as ET
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
        'SasukeIronclad/data/card_visual_map.json','SasukeIronclad/data/card_animation_manifest.json',
        'SasukeIronclad/data/card_name_overrides.json','SasukeIronclad/data/action_profiles.json',
        'SasukeIronclad/data/presentation_tiers.json','SasukeIronclad/data/presentation_surfaces.json',
        'SasukeIronclad/data/current_beta_card_scope.json',
        'docs/design/presentation-matrix.md','docs/design/card-specific-animation-system.md',
        'docs/design/card-renaming-system.md','docs/research/reference-chizuru-ironclad.md'
    ]
    for p in required:
        if not (ROOT/p).exists(): die(f"missing {p}")
    manifest=load('SasukeIronclad.json')
    if manifest.get('id')!='SasukeIronclad': die('manifest id changed')
    if manifest.get('author')!='Monster-Beast': die('manifest author must be Monster-Beast')
    if manifest.get('affects_gameplay') is not False: die('affects_gameplay must be false')
    dependencies=manifest.get('dependencies',[])
    baselib=[item for item in dependencies if item.get('id')=='BaseLib']
    if len(baselib)!=1 or baselib[0].get('min_version')!='3.3.0':
        die('BaseLib runtime minimum must remain the reviewed compatibility floor 3.3.0')

    cards=load('SasukeIronclad/data/card_visual_map.json')
    card_animations=load('SasukeIronclad/data/card_animation_manifest.json')
    card_names=load('SasukeIronclad/data/card_name_overrides.json')
    actions=load('SasukeIronclad/data/action_profiles.json')
    tiers=load('SasukeIronclad/data/presentation_tiers.json')
    surfaces=load('SasukeIronclad/data/presentation_surfaces.json')
    beta_scope=load('SasukeIronclad/data/current_beta_card_scope.json')
    if cards.get('schema_version')!=2: die('unsupported card schema')
    if card_animations.get('schema_version')!=1: die('unsupported card animation schema')
    if card_names.get('schema_version')!=1: die('unsupported card name schema')
    if actions.get('schema_version')!=2: die('unsupported action schema')
    if tiers.get('schema_version')!=2: die('unsupported tier schema')
    if surfaces.get('schema_version')!=1: die('unsupported surface schema')
    if beta_scope.get('schema_version')!=1: die('unsupported current beta card scope schema')
    if any(item.get('gameplay_changes') is not False for item in (cards,card_animations,card_names,tiers,beta_scope)):
        die('visual configuration declares gameplay changes')
    if actions.get('gameplay_timing_locked') is not True: die('action timing must remain locked')

    name_policy=card_names['policy']
    for key in ['display_only','internal_card_id_unchanged','preserve_rules_text','preserve_upgrade_state','fallback_to_original_name','allow_derived_cards']:
        if name_policy.get(key) is not True: die(f'card naming policy missing {key}')
    name_locales=name_policy.get('supported_locales',[])
    if len(name_locales)<2 or name_policy.get('default_locale') not in name_locales:
        die('card naming locales are incomplete')

    action_ids=unique(actions['profiles'],'id','action profile')
    for action in actions['profiles']:
        if action['fallback']!='original': die(f"invalid fallback for {action['id']}")
        if not 200 <= int(action['max_duration_ms']) <= 2000: die(f"invalid duration for {action['id']}")
        if not 0 <= float(action['impact_fraction']) <= 1: die(f"invalid impact fraction for {action['id']}")

    tier_ids=unique(tiers['tiers'],'id','presentation tier')
    ranks=[tier['rank'] for tier in tiers['tiers']]
    if len(ranks)!=len(set(ranks)): die('duplicate presentation tier rank')
    policy=tiers['default_policy']
    if policy.get('card_identity_first') is not True: die('card identity must be first')
    if policy.get('damage_selects_base_animation') is not False or policy.get('hit_count_selects_base_animation') is not False:
        die('damage or hit count cannot select the base animation')
    if policy.get('fast_mode_cap') not in tier_ids: die('presentation policy has unknown fast cap')
    if policy.get('low_flash_available') is not True: die('low-flash mode is required')
    for tier in tiers['tiers']:
        if not set(tier['development_fallback_profiles']) <= action_ids:
            die(f"tier {tier['id']} references unknown fallback action")
        if tier['id']=='finisher' and not tier.get('screen_takeover'): die('finisher must be screen takeover')

    animation_policy=card_animations['policy']
    required_true=['card_identity_is_primary_key','all_damage_cards_require_unique_timeline','damage_and_hits_are_variant_parameters_only','fallback_allowed_only_when_unverified_or_asset_failure']
    if any(animation_policy.get(key) is not True for key in required_true): die('card animation policy is incomplete')
    if animation_policy.get('damage_selects_base_animation') is not False or animation_policy.get('hit_count_selects_base_animation') is not False:
        die('card animation policy is damage-driven')
    allowed_modes=set(animation_policy['allowed_animation_modes'])
    required_damage_variants=set(animation_policy['required_variants_for_damage_cards'])
    animation_card_ids=unique(card_animations['animations'],'card_id','animation card id')
    unique(card_animations['animations'],'animation_id','animation id')
    animations_by_card={item['card_id']:item for item in card_animations['animations']}
    for animation in card_animations['animations']:
        if animation['animation_mode'] not in allowed_modes: die(f"unsupported animation mode for {animation['card_id']}")
        if animation['base_action_profile'] not in action_ids: die(f"unknown primitive for {animation['card_id']}")
        if animation['presentation_tier'] not in tier_ids: die(f"unknown tier for {animation['card_id']}")
        if animation.get('unique_timeline') is not True: die(f"card lacks unique timeline: {animation['card_id']}")
        if animation.get('damage_role')!='variant_parameter_only': die(f"damage selects base animation for {animation['card_id']}")
        expected_hit_sync='original_hit_events' if animation.get('is_damage_card') else 'timeline_local_events'
        if animation.get('hit_sync')!=expected_hit_sync:
            die(f"invalid impact synchronization for {animation['card_id']}: expected {expected_hit_sync}")
        if animation.get('fallback')!='original': die(f"invalid fallback for {animation['card_id']}")
        if len(animation.get('sequence',[]))<3: die(f"animation sequence is not rich enough: {animation['card_id']}")
        if animation.get('is_damage_card') and not required_damage_variants <= set(animation.get('variants',[])):
            die(f"damage card lacks required variants: {animation['card_id']}")

    surface_ids=unique(surfaces['surfaces'],'id','presentation surface')
    if len(surface_ids)<20: die('presentation surface scope is not rich enough')
    for surface in surfaces['surfaces']:
        if surface.get('required') and not surface.get('fallback'):
            die(f"required surface lacks fallback: {surface['id']}")

    card_ids=unique(cards['cards'],'card_id','card')
    if card_ids != animation_card_ids: die('card map and animation manifest differ')
    for card in cards['cards']:
        animation=animations_by_card[card['card_id']]
        if card['action_profile'] not in action_ids: die(f"unknown action for {card['card_id']}")
        if card['presentation_tier'] not in tier_ids: die(f"unknown tier for {card['card_id']}")
        if card['animation_id']!=animation['animation_id'] or card['animation_mode']!=animation['animation_mode']:
            die(f"animation metadata mismatch for {card['card_id']}")
        if card.get('is_damage_card') != animation.get('is_damage_card'): die(f"damage flag mismatch for {card['card_id']}")
        if card.get('is_damage_card') and card.get('special_animation_required') is not True:
            die(f"damage card does not require a special animation: {card['card_id']}")
        if card['legal_status']!='original_required': die(f"invalid legal status for {card['card_id']}")

    if beta_scope.get('status')!='pending_review': die('current beta card scope must remain pending_review')
    if beta_scope.get('branch')!='public-beta': die('current beta card scope must target public-beta')
    if not str(beta_scope.get('steam_build_id','')).isdigit(): die('current beta card scope buildid is invalid')
    fingerprint=beta_scope.get('fingerprint',{})
    for key in ['sts2_sha256','baselib_manifest_sha256']:
        if not re.fullmatch(r'[0-9a-f]{64}',str(fingerprint.get(key,''))): die(f'current beta card scope {key} is invalid')
    if not fingerprint.get('module_mvid') or not fingerprint.get('baselib_version'):
        die('current beta card scope fingerprint is incomplete')
    active_ids=unique(beta_scope.get('active_cards',[]),'card_id','active beta card')
    design_ids=unique(beta_scope.get('design_only_absent_cards',[]),'card_id','design-only beta card')
    if active_ids & design_ids: die('active and design-only beta card scopes overlap')
    if active_ids | design_ids != card_ids: die('current beta scope does not partition the card visual map')
    for item in beta_scope.get('active_cards',[]):
        if not str(item.get('model_type','')).startswith('MegaCrit.Sts2.Core.Models.Cards.'):
            die(f"invalid active model type for {item['card_id']}")
    for item in beta_scope.get('design_only_absent_cards',[]):
        if not str(item.get('expected_model_type','')).startswith('MegaCrit.Sts2.Core.Models.Cards.') or not item.get('reason'):
            die(f"invalid design-only evidence for {item['card_id']}")
    evidence=beta_scope.get('evidence',{})
    if evidence.get('two_runs_equivalent') is not True: die('current beta scope lacks equivalent two-run evidence')
    for key in ['run_1_sha256','run_2_sha256','comparison_sha256','attestation_sha256','binding_review_sha256']:
        if not re.fullmatch(r'[0-9a-f]{64}',str(evidence.get(key,''))): die(f'current beta scope evidence {key} is invalid')

    name_ids=unique(card_names['cards'],'card_id','card name override')
    normal_name_ids={item['card_id'] for item in card_names['cards'] if item.get('card_kind')=='normal'}
    if normal_name_ids != card_ids: die('normal card names do not cover current card map')
    display_by_locale={locale:set() for locale in name_locales}
    for item in card_names['cards']:
        if item.get('card_kind') not in {'normal','derived'}: die(f"invalid card name kind for {item['card_id']}")
        if item.get('card_kind')=='derived' and name_policy.get('allow_derived_cards') is not True:
            die(f"derived card name not allowed: {item['card_id']}")
        if item.get('rename_status') not in {'approved','provisional'}: die(f"invalid rename status: {item['card_id']}")
        if not item.get('semantic_anchor') or not item.get('art_concept'): die(f"missing naming concept: {item['card_id']}")
        for locale in name_locales:
            original=item.get('original_name',{}).get(locale,'').strip()
            display=item.get('display_name',{}).get(locale,'').strip()
            if not original or not display: die(f"missing localized card name: {item['card_id']} {locale}")
            if item.get('rename_status')=='approved' and original.casefold()==display.casefold():
                die(f"approved card was not renamed: {item['card_id']} {locale}")
            if display.casefold() in display_by_locale[locale]: die(f"duplicate display name: {display} {locale}")
            display_by_locale[locale].add(display.casefold())
    if 'GIANT_ROCK' not in name_ids: die('derived Giant Rock display name is missing')

    xml_trees={}
    for p in ['SasukeIronclad.csproj','Directory.Build.props','Sts2PathDiscovery.props']:
        try: xml_trees[p]=ET.parse(ROOT/p)
        except Exception as exc: die(f"invalid XML {p}: {exc}")
    project_root=xml_trees['SasukeIronclad.csproj'].getroot()
    if project_root.findtext('.//EnableDefaultCompileItems')!='false':
        die('mod project must disable recursive default Compile items')
    compile_includes=[item.get('Include') for item in project_root.findall('.//Compile') if item.get('Include')]
    if compile_includes!=['SasukeIroncladCode/**/*.cs']:
        die('mod project must compile only SasukeIroncladCode/**/*.cs')
    if any(value and ('.godot' in value or value.startswith('tools/')) for value in compile_includes):
        die('generated or tooling sources entered the production mod compile scope')
    package_names={item.get('Include') for item in project_root.findall('.//PackageReference')}
    if 'Alchyr.Sts2.BaseLib' in package_names:
        die('production build must not convert a floating BaseLib NuGet version into a runtime loader minimum')
    project_text=(ROOT/'SasukeIronclad.csproj').read_text(encoding='utf-8')
    if 'ActiveBaseLibVersion' in project_text or 'NuGetAssetsPath' in project_text:
        die('build must not rewrite the BaseLib runtime minimum from NuGet restore data')

    export_config=configparser.ConfigParser(interpolation=None)
    try:
        export_config.read(ROOT/'export_presets.cfg',encoding='utf-8')
        raw_exclusions=export_config.get('preset.0','exclude_filter')
        parsed_exclusions=json.loads(raw_exclusions)
        if not isinstance(parsed_exclusions,str):
            raise ValueError('exclude_filter must be a quoted string')
    except (configparser.Error,json.JSONDecodeError,ValueError) as exc:
        die(f'invalid export_presets.cfg exclude_filter: {exc}')
    excluded={item.strip() for item in parsed_exclusions.split(',') if item.strip()}
    required_exclusions={
        'local-audit*/*','audit-output/*','local-canary-*/*',
        'runtime-*/*','runtime-*.json','*.zip'
    }
    if not required_exclusions <= excluded:
        die('Godot export may include local audit or runtime evidence')

    forbidden={'.pck','.dll','.atlas','.skel','.ogg','.mp3','.ttf','.otf'}
    bad=[str(p.relative_to(ROOT)) for p in ROOT.rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix.lower() in forbidden]
    if bad: die('forbidden binary/extracted assets: '+', '.join(bad))
    damage_count=sum(1 for card in cards['cards'] if card['is_damage_card'])
    bespoke_count=sum(1 for animation in card_animations['animations'] if animation['animation_mode']=='bespoke')
    derived_names=sum(1 for item in card_names['cards'] if item.get('card_kind')=='derived')
    print(
        f"OK: {len(card_ids)} card timelines ({len(active_ids)} active beta, {len(design_ids)} design-only; "
        f"{damage_count} damage, {bespoke_count} bespoke), {len(name_ids)} display names ({derived_names} derived), "
        f"{len(action_ids)} primitives, {len(tier_ids)} tiers, {len(surface_ids)} surfaces; production compile scope isolated."
    )
    return 0


if __name__=='__main__': sys.exit(main())
