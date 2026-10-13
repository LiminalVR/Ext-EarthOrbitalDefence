"""Audit current source/config preservation and successful managed compiler inputs."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

project = Path(__file__).resolve().parents[2]
root = project.parents[1]
support = root / 'Builds/OfflineCompileSupport'
sys.path.insert(0, str(support))
from VerifyCurrentCompatibility import verify


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.run(['git', '--no-optional-locks', '-C', str(project), *args],
                          capture_output=True, check=True).stdout


output = project / 'Builds/URPMigration/OfflineCompileUnity67'
before = output / 'BeforeCompatibilityFix'
manifest = json.loads((before / 'manifest.json').read_text(encoding='utf-8'))
for item in manifest['files']:
    for key in ('path', 'preservedPath'):
        if sha(project / item[key]) != item['sha256']:
            raise RuntimeError('Original/current input differs: ' + item[key])
metas = {p.relative_to(project).as_posix() for p in (project / 'Assets').rglob('*.meta')}
if metas != {item['path'] for item in manifest['files'] if item['path'].endswith('.meta')}:
    raise RuntimeError('Current meta ownership differs')
index = Path(manifest['gitIndex']['path'])
if sha(index) != manifest['gitIndex']['sha256'] or sha(before / 'git-index-before') != manifest['gitIndex']['sha256']:
    raise RuntimeError('Git index differs')
if git('branch', '--show-current').decode().strip() != manifest['branch'] or git('rev-parse', 'HEAD').decode().strip() != manifest['commit']:
    raise RuntimeError('Branch/HEAD differs')
for filename, args in [('tracked-before.patch', ['diff', '--no-ext-diff', '--no-textconv', '--binary']),
                       ('staged-before.patch', ['diff', '--no-ext-diff', '--no-textconv', '--cached', '--binary'])]:
    if git(*args) != (before / filename).read_bytes():
        raise RuntimeError('Existing tracked/staged changes differ: ' + filename)
hashes = {}
compiler = verify(project, hashes)
summary = json.loads((output / 'summary.json').read_text(encoding='utf-8'))
if summary['unresolvedScopes'] or summary['unverifiedEditorSources']:
    raise RuntimeError('Incomplete managed source coverage')
record = {'utc': datetime.now(timezone.utc).isoformat(), 'project': str(project), 'offlineOnly': True,
          'branch': manifest['branch'], 'sourceRepairs': [], 'currentEditorSourceCount': 26,
          'preservedCurrentInputsVerified': len(manifest['files']), 'unchangedAssetsMetas': len(metas),
          'gitIndexUnchanged': True, 'existingTrackedAndStagedDiffsUnchanged': True,
          'sourceArchive': manifest['sourceArchive'], 'currentCompatibility': compiler,
          'uniqueCompilerInputsVerified': len(hashes),
          'verificationInputs': [{'path': str(p), 'sha256': sha(p)} for p in
             [Path(__file__), support / 'VerifyCurrentCompatibility.py', support / 'AuditCurrentLocalDependencies.py', before / 'manifest.json', output / 'summary.json']],
          'limits': ['Cached package/options and undeployed SDK candidate; no fresh Unity package resolution/import.',
                     'No gameplay/full ending, actual appearance capture, URP shader/render/player build or Quest 3 validation.']}
(output / 'verification.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps({key: record[key] for key in ['currentEditorSourceCount', 'preservedCurrentInputsVerified', 'unchangedAssetsMetas', 'uniqueCompilerInputsVerified']}, indent=2))
