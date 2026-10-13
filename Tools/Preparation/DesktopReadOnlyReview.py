"""Fresh preservation and source review; does not open Unity or alter source/evidence."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re, shutil, subprocess

project = Path(__file__).resolve().parents[2]
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
out = project / 'Builds/URPMigration' / ('DesktopPreparation-' + stamp)
out.mkdir(parents=True, exist_ok=False)
def sha(p):
    h = hashlib.sha256()
    with Path(p).open('rb') as f:
        for b in iter(lambda: f.read(1024 * 1024), b''): h.update(b)
    return h.hexdigest()
def git(*args):
    return subprocess.run(['git', '--no-optional-locks', '-C', str(project), *args], capture_output=True, check=True).stdout
def record(p):
    return {'path': p.relative_to(project).as_posix(), 'bytes': p.stat().st_size, 'sha256': sha(p)}
status = git('status', '--porcelain=v1', '-z')
(out / 'git-status-before.z').write_bytes(status)
for label, args in [('tracked', ['diff', '--no-ext-diff', '--no-textconv', '--binary']), ('staged', ['diff', '--no-ext-diff', '--no-textconv', '--cached', '--binary'])]:
    (out / (label + '-before.patch')).write_bytes(git(*args))
index = Path(git('rev-parse', '--path-format=absolute', '--git-path', 'index').decode().strip())
shutil.copy2(index, out / 'git-index-before')
dirty = []
entries = status.decode('utf-8').split('\0')
i = 0
while i < len(entries):
    entry = entries[i]; i += 1
    if not entry: continue
    flags, name = entry[:2], entry[3:]
    if 'R' in flags or 'C' in flags: i += 1
    p = project / name
    r = {'status': flags, 'path': name, 'exists': p.exists()}
    if p.is_file():
        dest = out / 'DirtyOriginals' / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, dest)
        r.update({'bytes': p.stat().st_size, 'sha256': sha(p), 'preservedPath': dest.relative_to(project).as_posix()})
        assert sha(dest) == r['sha256']
    dirty.append(r)
files = [record(p) for folder in ('Assets', 'Packages', 'ProjectSettings', 'Tools') for p in sorted((project / folder).rglob('*')) if p.is_file()]
historical = [record(p) for p in sorted((project / 'Builds').rglob('*')) if p.is_file() and not p.is_relative_to(out) and p.suffix in ('.json', '.cs', '.py', '.md', '.png', '.jpg')]
old = project / 'Builds/URPMigration/OfflineCompileUnity67'
manifest = json.loads((old / 'BeforeCompatibilityFix/manifest.json').read_text(encoding='utf-8-sig'))
original_checks = []
for r in manifest['files']:
    p = project / r['path']
    original_checks.append({'path': r['path'], 'expected': r['sha256'], 'actual': sha(p) if p.is_file() else None})
summary = json.loads((old / 'summary.json').read_text(encoding='utf-8-sig'))
compile_checks = []
for scope in summary['reports']:
    c = json.loads(Path(scope['report']).read_text(encoding='utf-8-sig'))
    rows = []
    for kind in ('sources', 'references', 'analyzers'):
        for r in c.get(kind, []):
            if not isinstance(r, dict) or 'path' not in r or 'sha256' not in r: continue
            p = Path(r['path'])
            rows.append({'kind': kind, 'path': str(p), 'expected': r['sha256'], 'actual': sha(p) if p.is_file() else None})
    compile_checks.append({'platform': scope['platform'], 'assembly': scope['assembly'], 'historicalErrors': scope['errors'], 'report': record(Path(scope['report'])), 'checks': rows})
plan_path = old / 'source-progression-plan.json'
plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
progression_checks = [{'path': r['path'], 'expected': r['sha256'], 'actual': sha(project / r['path'])} for r in plan['dependencyInventory']['inputs']]
guid_paths = {}
for m in (project / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', m.read_text(encoding='utf-8-sig', errors='replace'), re.M)
    if match: guid_paths[match[1]] = Path(str(m)[:-5])
materials = []
for r in plan['dependencyInventory']['inputs']:
    p = project / r['path']
    if p.suffix != '.mat': continue
    text = p.read_text(encoding='utf-8-sig')
    match = re.search(r'm_Shader: \{fileID: (-?\d+), guid: (\w+), type: (\d+)\}', text)
    source = guid_paths.get(match[2]) if match else None
    materials.append({**record(p), 'shaderPPtr': match[0] if match else None, 'localShaderPath': str(source) if source else None, 'requiresNativeIdentity': source is None})
look = project / 'Assets/Starfield Skybox/Demo/Script/LookCamera.cs'
look_guid = re.search(r'^guid: (\w+)', Path(str(look) + '.meta').read_text(), re.M)[1]
look_refs = []
for p in (project / 'Assets').rglob('*'):
    if p.suffix in ('.unity', '.prefab') and look_guid in p.read_text(encoding='utf-8-sig', errors='replace'):
        look_refs.append(record(p))
result = {'utc': datetime.now(timezone.utc).isoformat(), 'project': str(project), 'sourceOnly': True,
 'branch': git('branch', '--show-current').decode().strip(), 'commit': git('rev-parse', 'HEAD').decode().strip(),
 'gitIndex': {'path': str(index), 'sha256': sha(index)}, 'dirtyPreservation': dirty, 'currentFiles': files,
 'historicalEvidence': historical, 'original1073Checks': original_checks,
 'originalChangedInputs': [r for r in original_checks if r['expected'] != r['actual']],
 'historical26SourceManagedChecks': compile_checks, 'compileInputChanges': [r for s in compile_checks for r in s['checks'] if r['expected'] != r['actual']],
 'sourcePlan': record(plan_path), 'sourcePlan125DependencyChecks': progression_checks,
 'sourceProgressionChangedInputs': [r for r in progression_checks if r['expected'] != r['actual']],
 'sourceTimingAndAppearance': plan['sourceTimingAndAppearance'], 'weightedEnemyVariants': plan['weightedEnemyVariants'],
 'authoredDependencyMaterials': materials, 'legacyDemoInput': {'source': record(look), 'guid': look_guid, 'serializedReferenceFiles': look_refs},
 'nativeFirstTasks': ['Native source-first import; exact Editor Core/Release MonoScript/profile binding before Play/build.',
 'Verify existing Fire/PlayerAim SDK/XRI current modules, ordinary UI start, controller/hand ownership and no-device state separately.',
 'Interactive source smoke with genuine SDK/controller firing; timer/spawner/ending use WaitForEndOfFrame, unsupported in batch.',
 'One authored180-second survival route plus5-second clearing/full score writing/linger/audio/SDKEnd; actual Earth depletion path separately.',
 'Inventory exact shader property flags/GUID/localID/remaps and every renderer slot including trail/default/model materials before conversion.',
 'All3weighted real-spawn families; multiple approach/laser hit/collision/explosion/disappearance states; charged/depleted/recharge/no-hit/reticle states.',
 'Short source-native-candidate GPU proofs preserving0/0source lighting flags and source BRDF/ambient/reflection/blend/alpha/clock transport before scoped conversion.'],
 'limits': ['No Unity launch/import/Play/build or rendered acceptance. No production edits.',
 'Historical six managed scopes retained; reference-input drift is reported, not fresh SDK/provider closure.',
 'Material GUID dependency closure is an over-approximation and misses native embedded/default identities until import.',
 'LookCamera legacy input is only referenced in demo scene, not yet native reachability proof.',
 'Profile source ReleaseGUID is a known risk; native missing-class/binding claim remains unverified.',
 'Existing generic migration helper uses name-keyed extraction/broad save/stock GUI; do not invoke before scoped typed transport replaces it.']}
(out / 'review.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
assert sha(index) == result['gitIndex']['sha256']
print(json.dumps({'path': str(out / 'review.json'), 'sha256': sha(out / 'review.json'), 'currentFiles': len(files), 'dirtyRecords': len(dirty), 'originalChanges': len(result['originalChangedInputs']), 'compileInputChanges': len(result['compileInputChanges']), 'progressionChanges': len(result['sourceProgressionChangedInputs']), 'dependencyMaterials': len(materials), 'legacyDemoReferenceFiles': [r['path'] for r in look_refs]}, indent=2))
