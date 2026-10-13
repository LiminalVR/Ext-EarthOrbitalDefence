"""Preserve source-backed normal progression and appearance/instance requirements."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re

project = Path(__file__).resolve().parents[2]
scene = project / 'Assets/Scenes/The_Game.unity'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def blocks(path):
    text = path.read_text(encoding='utf-8-sig')
    return {m[2]: (m[1], m[3]) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}


def field(body, name):
    match = re.search(r'^  ' + re.escape(name) + r': (.*)$', body, re.M)
    return match[1] if match else None


scene_blocks = blocks(scene)
transforms = {}
for identifier, (kind, body) in scene_blocks.items():
    if kind in ('4', '224'):
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
        if go:
            transforms[go[1]] = (identifier, body)


def hierarchy(go):
    chain = []
    seen = set()
    while go and go != '0' and go not in seen:
        seen.add(go)
        body = scene_blocks.get(go, (None, None))[1]
        if body is None:
            return {'chain': chain, 'resolved': False, 'reason': 'Missing/stripped prefab GameObject'}
        chain.append({'gameObject': go, 'name': field(body, 'm_Name'), 'activeSelfSerialized': field(body, 'm_IsActive')})
        transform = transforms.get(go)
        if not transform:
            return {'chain': chain, 'resolved': False, 'reason': 'Missing/stripped transform'}
        father = re.search(r'm_Father: \{fileID: (-?\d+)\}', transform[1])
        if father and father[1] == '0':
            return {'chain': chain, 'resolved': True}
        parent = scene_blocks.get(father[1], (None, None))[1] if father else None
        parent_go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', parent or '')
        if not parent_go:
            return {'chain': chain, 'resolved': False, 'reason': 'Stripped parent requires source/override resolution'}
        go = parent_go[1]
    return {'chain': chain, 'resolved': False}


guid_paths = {}
for meta in (project / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig', errors='replace'), re.M)
    if match:
        guid_paths[match[1]] = Path(str(meta)[:-5])
records = []
for filename in ('Starter', 'AppController', 'SpawnSystem', 'Fire', 'Health', 'ScoreMenu', 'Reticule', 'WaveTimer', 'RespawnEnemys', 'StartGame', 'Enemy', 'Laser', 'EndExperience', 'PlayerAim'):
    path = project / 'Assets/Scripts' / (filename + '.cs')
    guid = re.search(r'^guid: (\w+)', path.with_suffix('.cs.meta').read_text(), re.M)[1]
    components = []
    for identifier, (kind, body) in scene_blocks.items():
        if kind == '114' and 'guid: ' + guid in body:
            go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
            components.append({'component': identifier, 'hierarchy': hierarchy(go[1]) if go else None,
                               'serializedComponent': body, 'serializedComponentSHA256': hashlib.sha256(body.encode()).hexdigest()})
    records.append({'path': path.relative_to(project).as_posix(), 'sha256': sha(path), 'guid': guid, 'directSceneComponents': components})

earth = project / 'Assets/Prefab/Earth.prefab'
earth_records = [{'id': identifier, 'serializedComponent': body} for identifier, (kind, body) in blocks(earth).items()
                 if kind == '114' and 'guid: a226d9bf96bc52045818695b2da76cd3' in body]
earth_override = scene_blocks['4649786932154586489'][1]
start_ui = scene_blocks['465749014'][1]
weighted = []
for guid, identifier, weight in [('a8f40263bcb315b4a9612fb12c0777ab', '4263326942169312124', 50),
                                 ('cd01d2320841dd0478e9f7545296fd1e', '7931566427520175567', 35),
                                 ('6870a305ec31a9e40a334443a4bb375e', '7871380609128387799', 15)]:
    path = guid_paths[guid]
    weighted.append({'guid': guid, 'path': path.relative_to(project).as_posix(), 'sha256': sha(path),
                     'enemyComponent': identifier, 'weightSerialized': weight, 'serializedComponent': blocks(path)[identifier][1]})

# GUID dependency closure is an inventory over-approximation, not callback/reachability proof.
pending = [scene] + [project / record['path'] for record in records]
visited = set()
unresolved = set()
text_extensions = {'.unity', '.prefab', '.mat', '.asset', '.controller', '.anim', '.shadergraph', '.shadersubgraph', '.shader', '.cs', '.asmdef', '.asmref'}
while pending:
    path = pending.pop()
    if path in visited or not path.is_file():
        continue
    visited.add(path)
    if path.suffix.lower() in text_extensions:
        for guid in set(re.findall(r'guid: ([0-9a-f]{32})', path.read_text(encoding='utf-8-sig', errors='replace'))):
            target = guid_paths.get(guid)
            if target and target.is_file():
                pending.append(target)
            elif guid != '00000000000000000000000000000000':
                unresolved.add(guid)
result = {'utc': datetime.now(timezone.utc).isoformat(), 'sourceOnly': True, 'runtimeVerified': False,
          'scene': {'path': scene.relative_to(project).as_posix(), 'sha256': sha(scene)}, 'scriptRecords': records,
          'startButton': {'component': '465749014', 'hierarchy': hierarchy('465749012'), 'serializedComponent': start_ui},
          'earthHealthPrefab': {'path': earth.relative_to(project).as_posix(), 'sha256': sha(earth), 'components': earth_records,
                                'sceneInstance': '4649786932154586489', 'serializedOverrides': earth_override,
                                'effectiveEarthHealthOverride': 300, 'prefabHealthDefault': 5},
          'weightedEnemyVariants': weighted,
          'normalPath': ['Starter.Start enables initially inactive Scene Objects; authenticate actual rig/UI and effective parent activity.',
                         'Use actual Start UI. Its persistent calls start SpawnSystem and AppController timer, then hide tutorial UI.',
                         'Aim through existing PlayerAim/SDK controller manager; ordinary VRButton.One or XRI logicalSelectState fires actual SphereCast damage/laser.',
                         'Survive authored180 game seconds or separately exercise real Earth depletion; allow5-second clearing wall, complete score writing/linger and audio/fader SDK End.'],
          'sourceTimingAndAppearance': {'maxGameLengthSeconds': 180, 'clearingWallSeconds': 5, 'scoreLingerSeconds': 5,
                                       'audioFadeSpeed': 1, 'enemySpawnDistance': 1000,
                                       'waveCountCurveEndpoints': [[0, 15], [180, 30]],
                                       'waveIntervalCurveKeys': [[0, 5], [60, 3], [140, 1], [180, 1]],
                                       'chargedLaserColor': [1, .7530315, 0, 1], 'drainedLaserColor': [1, 0, 0, 1],
                                       'laserWidths': [.25, .05], 'laserCooldownSeconds': .1, 'laserRadius': 15,
                                       'actualEndingDurationNotEstablished': True},
          'requiredCoverage': ['Natural matched60/120/180 game-time checkpoints, then full measured score/audio/fade/SDK completion. Separate real health-loss ending; do not extend authored timing.',
                               'All three weighted enemy prefabs and nested model/material/particle variants, with several real instances at spawn/approach/hit/impact/destruction/disappearance and changing useful camera views.',
                               'Player SphereCast beam/charged-red widths/material color/reticle refill, depleted/free-energy/recharge cycles, real hit and no-hit cases. Beam is hitscan, not a travelling projectile assertion.',
                               'Enemy private Fire has no current call or animation-event hit found; all three configured enemy Projectile references are null. Authenticate runtime reachability before inventing projectile coverage.',
                               'Fire.Start creates50 explosion/10 fire-effect prototypes. Only PlayerEffect(0) is called by current source; retained inactive fire prototypes do not prove actual muzzle instances. Resolve any dynamic callbacks separately.',
                               'Earth normal damage and full depletion explosion/UI, each actual explosion nested renderer/material/later phase, multiple pooled hit effects and5-second clearing-wall collateral collisions.',
                               'SpawnSystem waits when ActiveEnemies.Count>2 and uses integer evaluated curve intervals. Gameplay interaction/progress is needed; a timer-only run cannot prove late waves.',
                               'Review fresh URP-only transparency/emission/ambient/skybox/laser/particles/fog, runtime material/property writes and both no-shots/result and real firing scores.',
                               'Existing score accuracy divides by shotsFired; zero-shot result behavior is unverified. Enemy trigger/Kill assumes a valid SpawnSystem; clearing callbacks, cleanup and repeated effects require actual error/lifecycle checks.',
                               'Authenticate existing PrimaryInputDevice/hand-controller XRI aiming and SDK pointer/fader hashes after final SDK deployment; actual Android IL2CPP/GLES multiview/shader and Quest3 checks.'],
          'dependencyInventory': {'overApproximationOnly': True, 'inputs': [{'path': path.relative_to(project).as_posix(), 'sha256': sha(path)} for path in sorted(visited)],
                                  'unresolvedGUIDs': sorted(unresolved)},
          'limits': ['GUID closure/serialized overrides/source curves are planning, not rendered or runtime reachability proof. Package/built-in GUIDs may be outside Assets.',
                     'No scene/material/shader/texture/timing/input source was changed; original images, fresh package import and actual URP conversion/validation remain pending.']}
destination = project / 'Builds/URPMigration/OfflineCompileUnity67/source-progression-plan.json'
destination.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({'evidence': str(destination), 'scriptRecords': len(records), 'dependencyInputs': len(visited), 'weightedEnemyVariants': len(weighted)}, indent=2))
