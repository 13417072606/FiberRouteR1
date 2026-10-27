from pathlib import Path
import hashlib
root=Path(__file__).resolve().parents[1];out=root/'downloads/R1.3.2'
s=(root/'downloads/Download-R1.3.ps1').read_text(encoding='utf-8-sig')
s="param([string]$Folder = '', [switch]$NoOpen)\n"+s
s=s.replace("$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\\FiberRouteR1-R1.3'", "if (!$Folder) { $Folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\\FiberRouteR1-R1.3.2' }\n$folder = [IO.Path]::GetFullPath($Folder)")
s=s.replace("contents/downloads/R1.3/'","contents/downloads/'").replace("$artifactRef = 'r1-revised-20261025'","$artifactRef = '__ARTIFACT_COMMIT__'")
parts=[];packages=[]
for folder,name in [('R1.3.2','FiberRouteR1-win-x64-R1.3.2.zip'),('R1.3','FiberRouteR1-data-R1.3.zip')]:
    names=[]
    for p in sorted((root/'downloads'/folder).glob(name+'.part*')):
        data=p.read_bytes();parts.append("    @{ Folder = '%s'; Name = '%s'; Size = %s; Sha = '%s' }"%(folder,p.name,len(data),hashlib.sha256(data).hexdigest()));names.append("'%s'"%p.name)
    data=(root/'downloads'/folder/name).read_bytes();packages.append("    @{Name='%s'; Size=%s; Sha='%s'; Parts=@(%s)}"%(name,len(data),hashlib.sha256(data).hexdigest(),','.join(names)))
a=s.index('$parts = @(');b=s.index('\nforeach ($part',a);s=s[:a]+'$parts = @(\n'+',\n'.join(parts)+'\n)'+s[b:]
a=s.index('$packages = @(');b=s.index('\nforeach ($package',a);s=s[:a]+'$packages = @(\n'+',\n'.join(packages)+'\n)'+s[b:]
s=s.replace("$api + $part.Name + '?ref='", "$api + $part.Folder + '/' + $part.Name + '?ref='")
s=s.replace("'/downloads/R1.3/' + $part.Name", "'/downloads/' + $part.Folder + '/' + $part.Name")
s=s.replace('        Move-Item -LiteralPath $stage -Destination $run', '''        $oldRoot = Join-Path $stage '光路调度智能分析R1.3'
        $newRoot = Join-Path $stage '光路调度智能分析R1.3.2'
        foreach ($directory in @('数据','数据报告')) {
            $source = Join-Path $oldRoot $directory
            $destination = Join-Path $newRoot $directory
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            foreach ($file in Get-ChildItem -LiteralPath $source -Force) { Copy-Item -LiteralPath $file.FullName -Destination $destination -Recurse -Force }
        }
        Remove-Item -LiteralPath $oldRoot -Recurse -Force
        Move-Item -LiteralPath $stage -Destination $run''')
s=s.replace("$target = Join-Path $run '光路调度智能分析R1.3'","$target = Join-Path $run '光路调度智能分析R1.3.2'").replace('SUCCESS: R1.3 program','SUCCESS: R1.3.2 program').replace('Invoke-Item $target','if (!$NoOpen) { Invoke-Item $target }')
(out/'Download-R1.3.2.ps1').write_text(s,encoding='utf-8-sig')
print('Generated',len(parts),'verified download parts')
