"""Preserve exact current inputs before source-only Unity6.7 diagnostics."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil
import subprocess

project = Path(__file__).resolve().parents[2]
output = project / 'Builds/URPMigration/OfflineCompileUnity67'
before = output / 'BeforeCompatibilityFix'
if before.exists():
    raise RuntimeError('Current-input preservation already exists; do not overwrite.')
before.mkdir(parents=True)


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def git(*args):
    result = subprocess.run(['git', '-C', str(project), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace'))
    return result.stdout


branch = git('branch', '--show-current').decode().strip()
if branch != 'experiment/earth-orbital-defence-urp':
    raise RuntimeError('Unexpected branch: ' + branch)
gitdir = Path(git('rev-parse', '--git-dir').decode().strip())
if not gitdir.is_absolute():
    gitdir = (project / gitdir).resolve()
index = gitdir / 'index'
index_sha = sha(index) if index.is_file() else None
if index.is_file():
    shutil.copyfile(index, before / 'git-index-before')
(before / 'git-status-before.txt').write_bytes(git('status', '--porcelain', '-uall'))
for label, args in [('tracked-before', ['diff', '--no-ext-diff', '--no-textconv', '--binary']),
                    ('staged-before', ['diff', '--no-ext-diff', '--no-textconv', '--cached', '--binary'])]:
    result = subprocess.run(['git', '-C', str(project), *args], capture_output=True)
    (before / (label + '.patch')).write_bytes(result.stdout)
    (before / (label + '.stderr.txt')).write_bytes(result.stderr)
    if result.returncode:
        raise RuntimeError('Git preservation diff failed; preserved partial output: ' + label)
files = {path for path in (project / 'Assets').rglob('*') if path.is_file() and path.suffix in ('.cs', '.meta', '.asmdef', '.asmref')}
for folder in ('Packages', 'ProjectSettings', 'UserSettings'):
    files.update(path for path in (project / folder).rglob('*') if path.is_file())
rows = []
for path in sorted(files):
    relative = path.relative_to(project)
    destination = before / 'Originals' / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, destination)
    digest = sha(path)
    if sha(destination) != digest:
        raise RuntimeError('Preservation mismatch: ' + str(relative))
    rows.append({'path': relative.as_posix(), 'preservedPath': destination.relative_to(project).as_posix(), 'sha256': digest})
baseline = project / 'Builds/URPMigration/Baseline'
archive = baseline / 'project-before.tar'
archive_manifest = json.loads((baseline / 'archive-sha256.json').read_text(encoding='utf-8'))
archive_sha = sha(archive)
if archive_sha != archive_manifest['sha256']:
    raise RuntimeError('Original source archive checksum differs.')
record = {'utc': datetime.now(timezone.utc).isoformat(), 'project': str(project), 'branch': branch,
          'repository': git('rev-parse', '--show-toplevel').decode().strip(), 'commit': git('rev-parse', 'HEAD').decode().strip(),
          'gitIndex': {'path': str(index), 'existed': index.is_file(), 'sha256': index_sha},
          'sourceArchive': {'path': str(archive), 'bytes': archive.stat().st_size, 'sha256': archive_sha, 'freshlyVerified': True},
          'files': rows, 'currentCSharpCount': sum(path.suffix == '.cs' for path in files),
          'currentMetaCount': sum(path.suffix == '.meta' for path in files),
          'unityEditorLaunched': False, 'scope': 'Exact current code/meta/config state and prior source archive; actual appearance baseline remains pending.'}
(before / 'manifest.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps({key: record[key] for key in ('branch', 'sourceArchive', 'currentCSharpCount', 'currentMetaCount')}, indent=2))
