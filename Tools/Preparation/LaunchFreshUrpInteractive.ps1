$ErrorActionPreference='Stop'
$taskProject='D:/Work/Liminal/Platform/Upgrade/Limapps/49_Earth_Orbital_Defence'
$taskClosure=Get-Content -Raw -LiteralPath ($taskProject+'/Tools/URPMigration/ProductionClosure-current.json') | ConvertFrom-Json
$taskReportPath=$taskClosure.stage+'/report.json'
$taskClosureReport=Get-Content -Raw -LiteralPath $taskReportPath | ConvertFrom-Json
if($null -ne (Get-Process -Id 145120 -ErrorAction SilentlyContinue)){throw 'Old owned process still alive'}
foreach($taskInput in $taskClosureReport.inputs){
 if((Get-FileHash -LiteralPath ($taskProject+'/'+$taskInput.path) -Algorithm SHA256).Hash.ToLower() -ne $taskInput.sha256){throw ('Current sealed input drift: '+$taskInput.path)}
}
$taskEditors=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -notmatch '-adb2|-name AssetImportWorker'})
if($taskEditors.Count -gt 5){throw 'Total editor cap would be exceeded'}
if($taskEditors | Where-Object {$_.CommandLine -match [regex]::Escape('49_Earth_Orbital_Defence')}){throw 'Project already open'}
$taskMemory=Get-CimInstance Win32_OperatingSystem
if($taskMemory.FreePhysicalMemory -lt 12MB){throw 'Less than12GiB physical memory free'}
$taskStage=$taskProject+'/Builds/URPMigration/FreshUrpInteractive-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
New-Item -ItemType Directory -Path $taskStage | Out-Null
$taskExe='C:/Program Files/Unity/Hub/Editor/6000.7.0b2/Editor/Unity.exe'
$taskLog=$taskStage+'/editor-fresh-urp.log'
$taskArguments=@('-projectPath',$taskProject,'-force-d3d11','-buildTarget','Android','-accept-apiupdate','-acceptSoftwareTermsForThisRunOnly','-logFile',$taskLog)
$taskSafeEditors=@($taskEditors | ForEach-Object {@{pid=$_.ProcessId;project=if($_.CommandLine -match '-projectPath\s+(?:"([^"]+)"|(\S+))'){$Matches[1]+$Matches[2]}else{'Unknown; untouched'}}})
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
@{utc=[DateTime]::UtcNow.ToString('o');pid=$taskProcess.Id;project=$taskProject;exe=$taskExe;exeSHA256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash.ToLower();arguments=$taskArguments;log=$taskLog;freePhysicalKiB=$taskMemory.FreePhysicalMemory;editorsBefore=$taskSafeEditors;closure=$taskReportPath;closureSHA256=(Get-FileHash -LiteralPath $taskReportPath -Algorithm SHA256).Hash.ToLower();limits='Fresh graphics-enabled URP-only process; no Builtin diagnostics before full ordinary route. Source180 reused; no Setup/source timers/events/device changes.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($taskStage+'/launch.json')
@{stage=$taskStage;pid=$taskProcess.Id} | ConvertTo-Json | Set-Content -LiteralPath ($taskProject+'/Tools/URPMigration/SourceSession-current.json')
@{stage=$taskStage;pid=$taskProcess.Id;editorsBefore=$taskEditors.Count;editorsAfter=$taskEditors.Count+1;freePhysicalGiB=$taskMemory.FreePhysicalMemory/1MB} | ConvertTo-Json
