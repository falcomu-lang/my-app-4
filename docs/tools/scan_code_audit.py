"""Read every first-party code/config file and record reproducible audit coverage.
This is a static inventory, not execution coverage or a correctness test.
"""
from pathlib import Path
import hashlib, json, re, subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs' / 'audit-2026-11-04'
OUT.mkdir(exist_ok=True)
extensions = {'.cs', '.csproj', '.sln', '.config', '.ini', '.py', '.js', '.ps1'}
files = []
for folder in ('IntegratedImageProcessingApp', 'CameraCaptureModule', 'docs/tools'):
    for path in (ROOT / folder).rglob('*'):
        if not path.is_file() or path.suffix.lower() not in extensions:
            continue
        if any(part in ('bin', 'obj', '__pycache__', 'node_modules') for part in path.parts):
            continue
        if path.name in ('scan_code_audit.py', 'build_code_audit.py'):
            continue
        files.append(path)
files += [ROOT / 'MyApp4.sln', ROOT / 'SystemParameters.ini']
records = []
sources = {}
for path in sorted(set(files)):
    data = path.read_bytes()
    content = data.decode('utf-8-sig')
    relative = path.relative_to(ROOT).as_posix()
    sources[relative] = content
    records.append(dict(path=relative, lines=len(content.splitlines()), bytes=len(data),
        sha256=hashlib.sha256(data).hexdigest(),
        designer=path.name.endswith('.Designer.cs'),
        branches=len(re.findall(r'\b(?:if|for|foreach|while|catch|case)\b', content)),
        task_run=len(re.findall(r'\bTask\.Run\s*\(',content)),
        parallel=len(re.findall(r'\bParallel\.(?:For|ForEach|Invoke)\s*\(',content)),
        async_void=len(re.findall(r'\basync\s+void\b',content)),
        empty_catch=len(re.findall(r'catch(?:\s*\([^)]*\))?\s*\{\s*\}',content))))
projects = []
for path in (ROOT / 'IntegratedImageProcessingApp/IntegratedImageProcessingApp.csproj',
             ROOT / 'CameraCaptureModule/CameraCaptureModule.csproj'):
    tree = ET.parse(path)
    ns = {'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
    includes = [n.attrib['Include'] for n in tree.findall('.//m:Compile',ns)]
    listed = {(path.parent / name.replace('\\','/')).resolve() for name in includes}
    actual = {p.resolve() for p in path.parent.rglob('*.cs') if not any(x in ('bin','obj') for x in p.parts)}
    projects.append(dict(path=path.relative_to(ROOT).as_posix(), compile_count=len(includes),
        missing=[str(p.relative_to(ROOT)) for p in listed if not p.exists()],
        unlisted=[str(p.relative_to(ROOT)) for p in actual-listed]))
# Exact nonblank runs identify duplication candidates, never semantic equivalence.
runs = {}
for name, content in sources.items():
    if not name.endswith('.cs') or name.endswith('.Designer.cs'): continue
    meaningful = [(i+1,l.strip()) for i,l in enumerate(content.splitlines()) if l.strip() and not l.strip().startswith('//')]
    for i in range(max(0,len(meaningful)-19)):
        block=meaningful[i:i+20]
        key=hashlib.sha256('\n'.join(t for _,t in block).encode()).hexdigest()
        runs.setdefault(key,[]).append((name,block[0][0]))
dupes=[v for v in runs.values() if len({p for p,_ in v})>1]
cs = [r for r in records if r['path'].endswith('.cs')]
result=dict(date='2026-11-04', commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
    scope='First-party C#, project/config files and docs/tools code; excludes third-party packages and bin/obj generated outputs.',
    totals=dict(files=len(records),cs_files=len(cs),lines=sum(r['lines'] for r in records),cs_lines=sum(r['lines'] for r in cs),
        mainform_lines=sum(r['lines'] for r in cs if r['path'].startswith('IntegratedImageProcessingApp/Forms/MainForm')),
        async_void=sum(r['async_void'] for r in cs),empty_catch=sum(r['empty_catch'] for r in cs),task_run=sum(r['task_run'] for r in cs)),
    projects=projects, files=records, duplication_candidates=dupes[:100])
(OUT/'source-inventory.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('files','duplication_candidates')},ensure_ascii=False,indent=2))
print('Largest files:',[(r['path'],r['lines']) for r in sorted(cs,key=lambda x:-x['lines'])[:12]])
