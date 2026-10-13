from pathlib import Path
from datetime import datetime, timezone
import json,hashlib
p=Path(__file__).resolve().parents[2]
source=p/'Tools/URPMigration/Desktop49SourceRoute.cs';before=source.read_text();text=before.replace('Desktop49SourceRoute','Desktop49FinalUrpRoute')
text=text.replace('GraphicsSettings.currentRenderPipeline!=null','GraphicsSettings.currentRenderPipeline==null').replace('NormalSourcePlayrequired','FreshNormalURPPlayrequired')
text=text.replace('SourceSmoke-','FinalUrpSmoke-').replace('SourceRoute-','FinalUrpRoute-').replace('Observingordinarysource','ObservingordinaryURP').replace('ActualsourceUIpointerclick','ActualURPUIpointerclick')
text=text.replace('Naturalstochasticimages notmatchedURP. Sourcecamera explicitmono capture.','Naturalstochasticimages notmatchedsource. ActualownedMaincamera explicitmono capture; productionlightingbridge, no diagnostic registration.')
text=text.replace('c.Render();RenderTexture.active=rt;', '''
    bool beforeLinear=GraphicsSettings.lightsUseLinearIntensity,beforeTemp=GraphicsSettings.lightsUseColorTemperature;var beforeSpec=Shader.GetGlobalVector("_EarthOrbitalSourceIndirectSpecColor");long beforeBegin=EarthOrbitalSourceLightingScope.BeginCount,beforeEnd=EarthOrbitalSourceLightingScope.EndCount;
    var scopes=new JArray();Action<ScriptableRenderContext,Camera> onBegin=(ctx,cam)=>{if(cam==c)scopes.Add(new JObject{["linear"]=GraphicsSettings.lightsUseLinearIntensity,["temperature"]=GraphicsSettings.lightsUseColorTemperature,["ownedRoots"]=EarthOrbitalSourceLightingScope.RegisteredExperienceCount,["pending"]=EarthOrbitalSourceLightingScope.PendingScopes,["indirectSpec"]=new JArray(Shader.GetGlobalVector("_EarthOrbitalSourceIndirectSpecColor").x,Shader.GetGlobalVector("_EarthOrbitalSourceIndirectSpecColor").y,Shader.GetGlobalVector("_EarthOrbitalSourceIndirectSpecColor").z)});};
    RenderPipelineManager.beginCameraRendering+=onBegin;try{c.Render();}finally{RenderPipelineManager.beginCameraRendering-=onBegin;}
    if(scopes.Count!=1||scopes.Any(s=>(bool)s["linear"]||(bool)s["temperature"]||(int)s["ownedRoots"]<1)||EarthOrbitalSourceLightingScope.PendingScopes!=0||EarthOrbitalSourceLightingScope.BeginCount-beforeBegin!=EarthOrbitalSourceLightingScope.EndCount-beforeEnd||GraphicsSettings.lightsUseLinearIntensity!=beforeLinear||GraphicsSettings.lightsUseColorTemperature!=beforeTemp||Shader.GetGlobalVector("_EarthOrbitalSourceIndirectSpecColor")!=beforeSpec)throw new Exception("Actualownedcamera lighting/cleanup gate failed");
    events.Add(new JObject{["event"]="ActualOwnedMainCaptureScope",["label"]=label,["scopes"]=scopes,["beginDelta"]=EarthOrbitalSourceLightingScope.BeginCount-beforeBegin,["endDelta"]=EarthOrbitalSourceLightingScope.EndCount-beforeEnd,["restored"]=true});RenderTexture.active=rt;''')
assert 'GraphicsSettings.currentRenderPipeline==null' in text
dest=p/'Tools/URPMigration/Desktop49FinalUrpRoute.cs';dest.write_text(text)
play=p/'Tools/URPMigration/Native49PlayPreflight.cs'
playtext=play.read_text().replace('Native49PlayPreflight','Native49UrpPlayPreflight').replace('GraphicsSettings.currentRenderPipeline!=null','GraphicsSettings.currentRenderPipeline==null').replace('Stoppedcompiledsource required','StoppedcompiledURP required').replace('SourcePlay','FinalUrpPlay').replace('["sourcePipeline"]=true','["sourcePipeline"]=false')
(p/'Tools/URPMigration/Native49UrpPlayPreflight.cs').write_text(playtext)
out=p/'Builds/URPMigration'/('FinalUrpObserverPreparation-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
sha=lambda q:hashlib.sha256(q.read_bytes()).hexdigest()
(out/'report.json').write_text(json.dumps({'utc':datetime.now(timezone.utc).isoformat(),'sourceObserver':str(source),'sourceSHA256':sha(source),'currentObserver':str(dest),'currentSHA256':sha(dest),'changes':['Only pipeline guard/labels/class for freshURP route; source authentic UI/input/aim/physics/timing/capture progression reused.','Each actualMain capture asserts registered normal runtime root, production0/0 begin scope, balanced callbacks, zero pending scopes and exact external flag/ownedDC restoration. No diagnostic registration.'],'limits':['OutsideAssets Editor-only observer; no gameplay source/settings/package/SDK changes. No whole source replay.']},indent=2))
print(str(out/'report.json'))
