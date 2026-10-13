from pathlib import Path
p=Path(__file__).resolve().parents[2]
src=p/'Tools/URPMigration/Experience49Appearance.cs'
out=p/'Tools/URPMigration/Native49SourceAppearance.cs'
assert not out.exists()
s=src.read_text(encoding='utf-8-sig').replace('Experience49Appearance','Native49SourceAppearance')
needle='    public const string ScenePath'
assert s.count(needle)==1
s=s.replace(needle,'    public static string Current() { return Record("NativeSourceAppearance-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")); }\n'+needle,1)
out.write_text(s,encoding='utf-8')
print(str(out))
