from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re
p=Path(__file__).resolve().parents[2]
out=p/'Builds/URPMigration'/('LightingScopeCandidate-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
source=Path('D:/Work/Liminal/Platform/Upgrade/Limapps/167_Accelerate/Assets/Liminal/AccelerateSourceLightingScope.cs')
context=p/'Builds/URPMigration/LightingContext-20261003T004027805Z/report.json'
doc=json.loads(context.read_text());lda=p/doc['lightingDataAsset']['path'];dc=[doc['bakedAmbientProbe'][c][0] for c in range(3)]
def sha(x):return hashlib.sha256(x.read_bytes()).hexdigest()
s=source.read_text().replace('AccelerateSourceLightingScope','EarthOrbitalSourceLightingScope').replace('Assets/Scenes/Main.unity','Assets/Scenes/The_Game.unity').replace('AccelerateSourceBakedEnvironment','EarthOrbitalSourceBakedEnvironment').replace('_AccelerateSourceIndirectSpecColor','_EarthOrbitalSourceIndirectSpecColor')
s=s.replace('public Vector4 skyColor,skyPosition,indirectSpec;','public Vector4 indirectSpec;')
s=re.sub(r'    static readonly int SkyColor=.*?\n','',s);s=re.sub(r'    static readonly int SkyPosition=.*?\n','',s)
s=s.replace('skyColor=Shader.GetGlobalVector(SkyColor),skyPosition=Shader.GetGlobalVector(SkyPosition),','')
start=s.index('            var sun =');end=s.index('            var dc=',start);s=s[:start]+s[end:]
s=s.replace('            Shader.SetGlobalVector(SkyColor,sourceColor);\n','').replace('            Shader.SetGlobalVector(SkyPosition,new Vector4(sourceDirection.x,sourceDirection.y,sourceDirection.z,0));\n','')
s=s.replace('Shader.SetGlobalVector(SkyColor,frame.skyColor);Shader.SetGlobalVector(SkyPosition,frame.skyPosition);','')
(out/'EarthOrbitalSourceLightingScope.cs').write_text(s,encoding='utf-8')
provider='''// Authenticated own49 native LightingDataAsset.GetAmbientProbe() L0; no other experience constants.
public static class EarthOrbitalSourceBakedEnvironment {
 public const string LightingDataGUID="%s";
 public const string LightingDataSHA256="%s";
 public const string NativeSourceContextSHA256="%s";
 public static readonly UnityEngine.Vector3 SkyAmbientDC=new UnityEngine.Vector3(%sf,%sf,%sf);
}
'''%(doc['lightingDataAsset']['guid'],sha(lda),sha(context),*(format(x,'.10g') for x in dc))
(out/'EarthOrbitalSourceBakedEnvironment.cs').write_text(provider,encoding='utf-8')
controller=p/'Assets/Scripts/AppController.cs';originalBytes=controller.read_bytes();original=originalBytes.decode('utf-8-sig');newline='\r\n' if b'\r\n' in originalBytes else '\n';bom=b'\xef\xbb\xbf' if originalBytes.startswith(b'\xef\xbb\xbf') else b'';anchor='    private bool _timerActive;'
assert original.count(anchor)==1
hook='''    // Register the actual normal SDK experience root, including dynamic host loading.
    private Transform _sourceLightingOwner;
    private void OnEnable()
    {
        var owner = GetComponentInParent<Liminal.SDK.Core.ExperienceApp>(true);
        _sourceLightingOwner = owner == null ? null : owner.transform;
        EarthOrbitalSourceLightingScope.RegisterExperience(_sourceLightingOwner);
    }
    private void OnDisable()
    {
        EarthOrbitalSourceLightingScope.UnregisterExperience(_sourceLightingOwner);
        _sourceLightingOwner = null;
    }

'''
(out/'AppController.cs').write_bytes(bom+original.replace(anchor,hook.replace('\n',newline)+anchor).encode('utf-8'))
rows=[{'candidate':str(f),'candidateSHA256':sha(f)} for f in out.glob('*.cs')]
manifest={'utc':datetime.now(timezone.utc).isoformat(),'candidateOnly':True,'sourceBridge':str(source),'sourceBridgeSHA256':sha(source),'nativeOwnerContract':str(p/'Builds/URPMigration/OwnerContract-20261003T004659536Z/report.json'),'sourceContext':str(context),'sourceContextSHA256':sha(context),'sourceLightingData':str(lda),'sourceLightingDataSHA256':sha(lda),'ownNativeBakedL0':dc,'sourceController':str(controller),'sourceControllerSHA256':sha(controller),'rows':rows,'changes':['Remove all167sun/sky providers; use authenticated49scene/parent owner and own native bakedL0 only.','Normal AppController OnEnable/OnDisable registers actual GetComponentInParent<ExperienceApp> root; serialized gameplay fields/timings/UI/health/spawn callbacks unchanged.','Only owned camera scopes alter 0/0 flags and own prefixed fallback uniform; nested/endContext/domain/exit restore inherited bridge behavior.'],'limits':['Outside Assets candidate; current-source native compilation/runtime ownership/cleanup/cold first draw still gates.','Own reflection-off fallback uniform is derived from native own bake, but current12reachable Standard/spec identities reflect normally; no runtime reflection-off writer observed.','No source skybox compensation or shared SDK/global setting mutation.']}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2));(p/'Tools/URPMigration/LightingScopeCandidate-current.json').write_text(json.dumps({'stage':str(out)},indent=2));print(json.dumps({'manifest':str(out/'manifest.json'),'sha256':sha(out/'manifest.json')}))
