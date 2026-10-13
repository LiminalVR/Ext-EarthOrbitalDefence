$ErrorActionPreference='Stop'
$taskProject='D:/Work/Liminal/Platform/Upgrade/Limapps/49_Earth_Orbital_Defence'
$taskReview=Get-Content -Raw -LiteralPath ($taskProject+'/Builds/URPMigration/DesktopPreparation-20261002T233926522886Z/review.json') | ConvertFrom-Json
foreach($taskInput in $taskReview.original1073Checks) {
    if((Get-FileHash -LiteralPath ($taskProject+'/'+$taskInput.path) -Algorithm SHA256).Hash.ToLower() -ne $taskInput.expected){throw ('Original input drift: '+$taskInput.path)}
}
if((Get-FileHash -LiteralPath $taskReview.gitIndex.path -Algorithm SHA256).Hash.ToLower() -ne $taskReview.gitIndex.sha256){throw 'Index changed'}
$taskEditors=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -notmatch '-adb2|-name AssetImportWorker'})
if($taskEditors.Count -gt 5){throw 'Total editor cap would be exceeded'}
if($taskEditors | Where-Object {$_.CommandLine -match [regex]::Escape('49_Earth_Orbital_Defence')}){throw 'Project already open'}
$taskStage=$taskProject+'/Builds/URPMigration/SourceInteractiveSession-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
New-Item -ItemType Directory -Path $taskStage | Out-Null
$taskExe='C:/Program Files/Unity/Hub/Editor/6000.7.0b2/Editor/Unity.exe'
$taskLog=$taskStage+'/editor-source-interactive.log'
$taskArguments=@('-projectPath',$taskProject,'-force-d3d11','-buildTarget','Android','-accept-apiupdate','-acceptSoftwareTermsForThisRunOnly','-logFile',$taskLog)
$taskMemory=Get-CimInstance Win32_OperatingSystem
$taskSafeEditors=@($taskEditors | ForEach-Object {@{pid=$_.ProcessId;project=if($_.CommandLine -match '-projectPath\s+(?:"([^"]+)"|(\S+))'){$Matches[1]+$Matches[2]}else{'Unknown; untouched'}}})
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
@{utc=[DateTime]::UtcNow.ToString('o');pid=$taskProcess.Id;project=$taskProject;exe=$taskExe;exeSHA256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash.ToLower();arguments=$taskArguments;log=$taskLog;freePhysicalKiB=$taskMemory.FreePhysicalMemory;editorsBefore=$taskSafeEditors;preservation=$taskProject+'/Builds/URPMigration/DesktopPreparation-20261002T233926522886Z/review.json';preservationSHA=(Get-FileHash -LiteralPath ($taskProject+'/Builds/URPMigration/DesktopPreparation-20261002T233926522886Z/review.json') -Algorithm SHA256).Hash.ToLower();limits='Source-first interactive D3D11 Android. No Setup/conversion; native binding/gameplay/render/player checks pending.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($taskStage+'/launch.json')
@{stage=$taskStage;pid=$taskProcess.Id} | ConvertTo-Json | Set-Content -LiteralPath ($taskProject+'/Tools/URPMigration/SourceSession-current.json')
@{stage=$taskStage;pid=$taskProcess.Id;editorsBefore=$taskEditors.Count;editorsAfter=$taskEditors.Count+1;freePhysicalGiB=$taskMemory.FreePhysicalMemory/1MB} | ConvertTo-Json
