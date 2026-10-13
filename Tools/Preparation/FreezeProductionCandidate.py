"""Freeze an outside-Assets, source-authenticated scoped conversion packet. No deployment."""
from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, shutil, re, subprocess
p = Path(__file__).resolve().parents[2]
out = p/'Builds/URPMigration'/('ProductionCandidate-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
out.mkdir()
def sha(x): return hashlib.sha256(x.read_bytes()).hexdigest()
def stage(name): return Path(json.loads((p/f'Tools/URPMigration/{name}-current.json').read_text())['stage'])
scope = p/'Builds/URPMigration/ReachableScope-20261003T010706071Z/report.json'
doc = json.loads(scope.read_text()); identities = {}
def walk(x):
    if isinstance(x, dict):
        if x.get('nativeJSON') and x.get('shader') != 'Skybox/6 Sided': identities.setdefault((x['guid'], x['localID']), x)
        for v in x.values(): walk(v)
    elif isinstance(x, list):
        for v in x: walk(v)
walk(doc)
assert len(identities) == 29
pbr, particles, lighting = stage('PbrCandidate'), stage('ParticleCandidates'), stage('LightingScopeCandidate')
production = out/'Production'; rows = []; copied = []
def put(src, dst):
    target = production/dst; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(src, target)
    copied.append({'source':str(src),'sourceSHA256':sha(src),'candidate':str(target),'candidateSHA256':sha(target),'deployPath':dst})
for src in pbr.iterdir():
    if src.suffix not in ['.shader','.hlsl']: continue
    dest = 'Assets/Shaders/Source49/Stock/'+src.name
    text = src.read_text().replace(pbr.as_posix(), 'Assets/Shaders/Source49/Stock')
    target = production/dest; target.parent.mkdir(parents=True,exist_ok=True); target.write_text(text,encoding='utf-8')
    copied.append({'source':str(src),'sourceSHA256':sha(src),'candidate':str(target),'candidateSHA256':sha(target),'deployPath':dest,'change':'Only outside-Assets include locator replaced by owned production location.'})
for src in particles.iterdir():
    if src.suffix not in ['.shader','.hlsl']: continue
    dest = 'Assets/Shaders/Source49/Particles/'+src.name
    text=src.read_text().replace(particles.as_posix(), 'Assets/Shaders/Source49/Particles').replace(pbr.as_posix(), 'Assets/Shaders/Source49/Stock')
    target=production/dest; target.parent.mkdir(parents=True,exist_ok=True); target.write_text(text,encoding='utf-8')
    copied.append({'source':str(src),'sourceSHA256':sha(src),'candidate':str(target),'candidateSHA256':sha(target),'deployPath':dest,'change':'Only outside-Assets include locator replaced by owned production location.'})
for candidate in (production/'Assets/Shaders').rglob('*'):
    if candidate.is_file():
        assert not re.search(r'#include\s+"(?:[A-Za-z]:|/|[^"\n]*Builds/URPMigration)',candidate.read_text()), candidate
for name in ['EarthOrbitalSourceLightingScope.cs','EarthOrbitalSourceBakedEnvironment.cs']:
    put(lighting/name, 'Assets/Scripts/'+name)
put(lighting/'AppController.cs','Assets/Scripts/AppController.cs')
put(p/'Tools/URPMigration/Native49ScopedProduction.cs','Tools/URPMigration/Native49ScopedProduction.cs')
families={
    'Standard':'Stock/LitSourceProperties.shader', 'Standard (Specular setup)':'Stock/LitSourceProperties.shader',
    'Legacy Shaders/Particles/Additive':'Particles/LegacyParticleAdditive.shader',
    'Legacy Shaders/Particles/Alpha Blended':'Particles/LegacyParticleAlpha.shader',
    'Legacy Shaders/Particles/Alpha Blended Premultiply':'Particles/LegacyParticlePremultiply.shader',
    'Particles/Standard Unlit':'Particles/ParticlesStandardUnlitRGB.shader',
    'Particles/Standard Surface':'Particles/ParticlesLitSourceRGBA.shader',
}
for (guid, local), material in identities.items():
    row=dict(material); row['convert']=row['shader'] in families
    if row['convert']:
        row['targetShader']='Assets/Shaders/Source49/'+families[row['shader']]
        if not row['path'].lower().endswith('.mat'):
            safe=re.sub('[^a-zA-Z0-9_-]+','_',row['name'])
            row['targetMaterial']=f'Assets/URPMigration/Source49/Materials/{guid}_{local}_{safe}.mat'
    else: row['reason']='Retain native Unlit Color MainColor runtime writer/Sprites/SDK reticles; own controlled GPU RGBA proof.'
    rows.append(row)
assert sum(r['convert'] for r in rows)==25
serialized=[]
# Select only files with exact authenticated source identity literals; native references are checked again after deployment.
patterns=[re.compile(r'\{fileID: '+re.escape(str(r['localID']))+', guid: '+r['guid']+r', type: [023]\}') for r in rows if r['convert'] and not r['path'].endswith('.mat')]
for file in (p/'Assets').rglob('*'):
    if file.suffix not in ['.unity','.prefab']: continue
    data=file.read_bytes()
    try: text=data.decode('utf-8')
    except UnicodeDecodeError: continue
    if any(pattern.search(text) for pattern in patterns): serialized.append(file.relative_to(p).as_posix())
inputs=[]
for folder in ['Assets','ProjectSettings','Packages']:
    for file in sorted((p/folder).rglob('*')):
        if not file.is_file(): continue
        relative=file.relative_to(p); dest=out/'PreservedSource'/relative
        dest.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(file,dest)
        record={'path':relative.as_posix(),'sha256':sha(file),'bytes':file.stat().st_size,'preserved':str(dest)}
        if relative.as_posix()=='Assets/Scripts/AppController.cs': record['allowedDeployedSHA256']=sha(production/relative)
        inputs.append(record)
gitdir=subprocess.check_output(['git','rev-parse','--git-path','index'],cwd=p,text=True).strip()
index=Path(gitdir); index=index if index.is_absolute() else p/index
shutil.copy2(index,out/'source-index'); entries=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p)
(out/'source-stage-entries.bin').write_bytes(entries)
status=subprocess.check_output(['git','--no-optional-locks','status','--porcelain=v1','-z'],cwd=p);(out/'source-status.bin').write_bytes(status)
original=(p/'Assets/Scripts/AppController.cs').read_bytes(); hooked=(production/'Assets/Scripts/AppController.cs').read_bytes()
hooked_text=hooked.decode('utf-8-sig'); original_text=original.decode('utf-8-sig')
start=hooked_text.index('    // Register the actual normal SDK experience root')
end=hooked_text.index('    private bool _timerActive;',start)
assert hooked_text[:start]+hooked_text[end:]==original_text
assert hooked.count(b'\r\n')==original.count(b'\r\n')+(hooked_text[start:end].count('\n') if b'\r\n' in original else 0)
dependencies=[]
for report in [scope,pbr/'manifest.json',particles/'manifest.json',lighting/'manifest.json',p/'Builds/URPMigration/CandidateControlsReview-20261003T013134372192Z/review.json',p/'Builds/URPMigration/PbrNativeCompile-20261003T005737245Z/report.json',p/'Builds/URPMigration/ParticleNativeCompile-20261003T012706543Z/report.json']:
    dependencies.append({'path':str(report),'sha256':sha(report)})
manifest={
    'schema':1,'utc':datetime.now(timezone.utc).isoformat(),'candidateOnly':True,'project':str(p),'pid':145120,'packetPath':str(out/'manifest.json'),
    'scope':str(scope),'scopeSHA256':sha(scope),'materialIdentities':rows,'identities':29,'convert':25,'keep':4,
    'productionFiles':copied,'serializedReferenceFiles':sorted(serialized),'preservedInputs':inputs,'evidence':dependencies,
    'index':{'preserved':str(out/'source-index'),'sha256':sha(out/'source-index'),'stagingEntries':str(out/'source-stage-entries.bin'),'stagingEntriesSHA256':sha(out/'source-stage-entries.bin')},
    'controllerHook':{'originalSHA256':hashlib.sha256(original).hexdigest(),'candidateSHA256':hashlib.sha256(hooked).hexdigest(),'onlyInsertedHook':True,'BOMAndExistingLineEndingsPreserved':True},
    'plan':['Exact shader/runtime bridge import and genuine native compilation before scoped transfer; no broad Setup/Open/generic converter.',
        'Convert only25 exact source identities, save individually; four embedded FBX copies/remaps plus one builtin Default Particle local copy.',
        'Rewrite exact embedded/default PPtrs before ModelImporter reimport; audit every original ordered renderer slot and authored null after.',
        'Forward, MSAA1, actual Linear falloff enum1, additional pixel limit4; source particle Surface retains four vertex diffuse contributions/no ForwardAdd.',
        'Restore source keywords/emission intent/GI/double-sided/instancing/queue; own source primary _Color/_MainTex and Gamma-metallic/active smoothness preserved.',
        'Authenticate native disk/effective typed/remap/slot closure before source-free cold URP Main and one final authored180s survival/ending.',
        'Final Android/native target providers/actual packed Release ExperienceProfile fields remain separate.'],
    'limits':['Outside Assets only, not native conversion/production import/URP acceptance.',
        'Particle distortion opaque texture cannot reproduce arbitrary prior transparent GrabPass composition; actual Main affected-region review required.',
        'PBR point source additive alpha2 vs owned onepass alpha1 and nonzero rawHDR edge differences retained; meaningful RGB reviewed, not exactRGBA claim.',
        '42 genuine nonempty PBR GLES programs; earlier42 unsupported D3D+Android requests remain failed. Additional PBR point GLES selector subset pending.',
        '66 genuine nonempty final particle GLES programs; actual Android player/XR hardware pending.',
        'Source skybox6-sided and retained native SDK/Sprites/Unlit families need cold production whole-camera compatibility; no generic parity claim.',
        'Source full193.733 survival route reused; final URP natural route/normal depletion point/lifecycle remain future acceptance. Source UI partial captures retained.',
        'Snapshot raw index and staging entries preserved, no staging/index restore. Shared SDK/other projects untouched.']}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(p/'Tools/URPMigration/ProductionCandidate-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'manifest':str(out/'manifest.json'),'sha256':sha(out/'manifest.json'),'preservedInputs':len(inputs),'identities':29,'convert':25,'serializedReferenceFiles':len(serialized),'productionFiles':len(copied)},indent=2))
