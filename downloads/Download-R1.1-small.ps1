$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.1'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.1-small/'
$artifactRef = 'r1-revised-20261025'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$curl = Get-Command curl.exe -CommandType Application -ErrorAction SilentlyContinue
function Test-Part($path, $size, $sha) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne $size) { return $false }
    return ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha)
}
$parts = @(
    @{ Name = 'R1.1.chunk001'; Size = 2000000; Sha = '21ffe70e88dfa702986e371069173d9be7a4d52d31c88b3dbfdb5820d601e366' },
    @{ Name = 'R1.1.chunk002'; Size = 2000000; Sha = '6fb1556fce7adb40962f9aca08735922f25ebc529e8e2d04479f41765c96a6e6' },
    @{ Name = 'R1.1.chunk003'; Size = 2000000; Sha = '3412c2f7d4339f89c6ac298eb7238f19883790e33bb91809cc1e5aef471a6ffb' },
    @{ Name = 'R1.1.chunk004'; Size = 2000000; Sha = 'f483f6c9396d5d8841bc10e51f38a8a1e2c638892d805558adf063a1f09679e1' },
    @{ Name = 'R1.1.chunk005'; Size = 2000000; Sha = 'b6955c1bab93664b575deff0528feaeacc549ed577bd69327bd5a56dbbd0df5a' },
    @{ Name = 'R1.1.chunk006'; Size = 2000000; Sha = '873a3386abebdacb1db0aa275d8006dc0ffcf06dc6f4e2328d3899956d223349' },
    @{ Name = 'R1.1.chunk007'; Size = 2000000; Sha = '546b993cc432468ab9dd5ecf251acea322eff24204d67c09ceee96dfa5a4a4e0' },
    @{ Name = 'R1.1.chunk008'; Size = 2000000; Sha = 'be12ea54f2a23588f564d5c7fe7d7545235e705abd61fceec97b11c9caed7210' },
    @{ Name = 'R1.1.chunk009'; Size = 2000000; Sha = '2e00505185e68f0e35723dc2edf1115a9cd42c20d7160090d0e858e205719187' },
    @{ Name = 'R1.1.chunk010'; Size = 2000000; Sha = '01b8aa0328978d0171e0b393b1fb09f234e51a76c9e97f9c2553c6b0108bd9c9' },
    @{ Name = 'R1.1.chunk011'; Size = 2000000; Sha = 'dd610880722692cfe4304adc35ee06eaf74914379e55f7ee0dab96197b9f2315' },
    @{ Name = 'R1.1.chunk012'; Size = 2000000; Sha = '77b09add665f3df1ed5b586a7b5307f4ff8f1d596505ccb989314f66ff68b69a' },
    @{ Name = 'R1.1.chunk013'; Size = 2000000; Sha = 'f01a5debc2e78463c194bd3a1a7cbf149ae35f63d50373d0cfefef2674802378' },
    @{ Name = 'R1.1.chunk014'; Size = 2000000; Sha = 'aac8617af0a4568d0146d75afaacdd5b8f2a55b180afe3c2a569fda024a69c47' },
    @{ Name = 'R1.1.chunk015'; Size = 2000000; Sha = '961ac4fbb21b07cda14161053511cf39a4283a918f097138824910484213d9fe' },
    @{ Name = 'R1.1.chunk016'; Size = 2000000; Sha = 'd3a3853f79156e766142db1b514ade2654f8476d7db35b220a1b0d08e2f4a615' },
    @{ Name = 'R1.1.chunk017'; Size = 2000000; Sha = 'f07c8c24f00a62feae320b8b8e9c875d4a9e3606cf413aff428fadd850e84663' },
    @{ Name = 'R1.1.chunk018'; Size = 2000000; Sha = '653bbb847d57e0b59b25a96493d08f842493775e7f0e0ad6518988d940f4a52e' },
    @{ Name = 'R1.1.chunk019'; Size = 2000000; Sha = '3a95137c9498b27abe25490dae490bd1c35fee31639e5c65b2ced408a2c6b613' },
    @{ Name = 'R1.1.chunk020'; Size = 2000000; Sha = 'e6708aa468aa1de517c3c265e6dbd8bc57edb16e5c07c680379aeb06e59a6510' },
    @{ Name = 'R1.1.chunk021'; Size = 2000000; Sha = '641d0bd38c5366337f1d60c191516998656672d069a1f26bce2eda1687e9dc35' },
    @{ Name = 'R1.1.chunk022'; Size = 2000000; Sha = '10160707484d8c85d3803ca2c47eb8f64a4172d8b6e709d73c11c2ef305d3e79' },
    @{ Name = 'R1.1.chunk023'; Size = 2000000; Sha = '99e45868ae49632d036d7a48eed341730103159e025af9b7dbb43aa9f037e1e8' },
    @{ Name = 'R1.1.chunk024'; Size = 2000000; Sha = '48b11f9f18ce8569fa80b220c994660df6db4c73136ba5327fa839a74cb8673e' },
    @{ Name = 'R1.1.chunk025'; Size = 2000000; Sha = '5e2e6043cd524846f3c2f18ed98b8e0180ac93e31e939dee8aa550574072e358' },
    @{ Name = 'R1.1.chunk026'; Size = 2000000; Sha = '402c0b94df959ba1fc7da4982597b01c6b40d6c372ff60a216d8283e39b46f45' },
    @{ Name = 'R1.1.chunk027'; Size = 2000000; Sha = '5542aed05cd50baa22cc1ccb662adf3c333479afc15aad94cfa284708348be15' },
    @{ Name = 'R1.1.chunk028'; Size = 2000000; Sha = 'b30c94272f61a1cf2397efb557d82522ca69ab727ba1fd6fa9a2ae1ad304d236' },
    @{ Name = 'R1.1.chunk029'; Size = 2000000; Sha = '50a162cee4a8107e1819f14145e29ee9fae401c757b306fc81669198ae995d95' },
    @{ Name = 'R1.1.chunk030'; Size = 2000000; Sha = '1b8a8dd3b6882df3e33db96dcd66fb073656aeb8409ee6e3a265545cb7b4d51a' },
    @{ Name = 'R1.1.chunk031'; Size = 2000000; Sha = '15f982adb036395c57c9ea1057e5330b33da975cbb0d80d3f2f5246ed531f2fd' },
    @{ Name = 'R1.1.chunk032'; Size = 2000000; Sha = 'a5d7fb10eb2857bcc6b0f9e390fe660701566391c2956e44f6852c805af1cc8b' },
    @{ Name = 'R1.1.chunk033'; Size = 2000000; Sha = '603b83f5d29bb3a4539c95f97bbc0e3651326363a2b53cbb6df73d65d4bee7b0' },
    @{ Name = 'R1.1.chunk034'; Size = 2000000; Sha = '58b0fcb233371a4163efe74ab256159e49ad0100140b8ce5201bc55dfdb93b12' },
    @{ Name = 'R1.1.chunk035'; Size = 2000000; Sha = 'af10dfe631c8c286dbcf6b46c9a0ab885cc9bff2187efd096b8d2fdc73d8be08' },
    @{ Name = 'R1.1.chunk036'; Size = 2000000; Sha = '55e01c24519941b7347e04af1f329b0e33fbb46898387eb5e19a3f487a4843e9' },
    @{ Name = 'R1.1.chunk037'; Size = 2000000; Sha = 'b6dd3588e9fd371eebe01b2195e3690dab1e49448f76c94805e4a0e25aca3e64' },
    @{ Name = 'R1.1.chunk038'; Size = 2000000; Sha = 'a418283d6c0770eb949604ce9dfeddf30556c788863df658fa88fedbd6d3b8f1' },
    @{ Name = 'R1.1.chunk039'; Size = 2000000; Sha = 'a8d0ed1a4b1f13e93c967bb744d171e81707abe3a38b018a0ac980794de03f45' },
    @{ Name = 'R1.1.chunk040'; Size = 2000000; Sha = '5ffca51d6a3fadbf14de5a4c5b166f0e86eb94f5885f65280e8a719e5cc85bef' },
    @{ Name = 'R1.1.chunk041'; Size = 2000000; Sha = 'f707dc905aead63051333c782c045f72ce80c86c04dfb40c84ef78552df3361b' },
    @{ Name = 'R1.1.chunk042'; Size = 2000000; Sha = '2ad50cd2817953965a0ce8f8f327532c7715b58e7213f08aaca7fe7559210095' },
    @{ Name = 'R1.1.chunk043'; Size = 2000000; Sha = '83bf1e9fae5dca73a151159cc4f53962c1bf55759137813cc723581dafd55852' },
    @{ Name = 'R1.1.chunk044'; Size = 2000000; Sha = '4a926884442863f7261ec2d6d0215a6af371be38ce2bbe8cc20437fd7d5d6ccd' },
    @{ Name = 'R1.1.chunk045'; Size = 2000000; Sha = '1e874f221e08a6c08ae29a218012f6a70d0b2861f98e9ac7da1f90c4564ad24b' },
    @{ Name = 'R1.1.chunk046'; Size = 2000000; Sha = 'b9ada7f3f7e4188a95cf2e25bd787fa7185d04dc7b4dd4354be700b1f98d0cda' },
    @{ Name = 'R1.1.chunk047'; Size = 2000000; Sha = '8d9541b853f05246e42919117605d4e65e997da84aebd4dad714aff35d0ccaf9' },
    @{ Name = 'R1.1.chunk048'; Size = 75979; Sha = '991996eed9403fc7480f4fe594d13a3211b5877b086a76124773201a7e88514f' }
)
foreach ($part in $parts) {
    $dest = Join-Path $folder $part.Name
    if (Test-Part $dest $part.Size $part.Sha) {
        Write-Host ('Already verified: ' + $part.Name)
        continue
    }
    $temporary = $dest + '.partial'
    $uri = $api + $part.Name + '?ref=' + $artifactRef
    $verified = $false
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            Write-Host ('Downloading {0}, attempt {1}/4' -f $part.Name, $attempt)
            if ($curl) {
                & $curl.Source --fail --location --http1.1 --tlsv1.2 --connect-timeout 30 --max-time 600 --header 'Accept: application/vnd.github.raw+json' --user-agent 'FiberRouteR1-Downloader' --output $temporary $uri
                if ($LASTEXITCODE -ne 0) { throw ('curl failed, exit code ' + $LASTEXITCODE) }
            } else {
                Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers $headers -OutFile $temporary -TimeoutSec 600
            }
            if (!(Test-Part $temporary $part.Size $part.Sha)) { throw 'Part size or SHA256 verification failed.' }
            Move-Item -LiteralPath $temporary -Destination $dest -Force
            $verified = $true
            Write-Host ('Verified: ' + $part.Name)
            break
        } catch {
            Write-Warning $_.Exception.Message
            if ($attempt -lt 4) { Start-Sleep -Seconds 3 }
        }
    }
    if (!$verified) {
        Remove-Item -LiteralPath $temporary -ErrorAction SilentlyContinue
        throw ('Download stopped. Verified 2MB chunks are preserved. If HTTP 403 reports an API rate limit, wait for the limit to reset, then rerun. Failed chunk: ' + $part.Name)
    }
}
$zip = Join-Path $folder 'FiberRouteR1-win-x64-R1.1.zip'
$merged = $zip + '.partial'
$outputStream = [IO.File]::Create($merged)
try {
    foreach ($part in $parts) {
        $partStream = [IO.File]::OpenRead((Join-Path $folder $part.Name))
        try { $partStream.CopyTo($outputStream) } finally { $partStream.Dispose() }
    }
} finally { $outputStream.Dispose() }
$expected = '1011ea4aac733c1491630adf092305f62ebd0ab49a2eb8fce2ada010188aa173'
if (!(Test-Part $merged 94075979 $expected)) { throw 'Merged ZIP verification failed. Do not use it.' }
Move-Item -LiteralPath $merged -Destination $zip -Force
Write-Host ''
Write-Host ('SUCCESS: ZIP downloaded and SHA256 verified: ' + $zip) -ForegroundColor Green
Invoke-Item $folder
