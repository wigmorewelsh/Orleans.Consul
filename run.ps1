#env pswh

$consul = "https://releases.hashicorp.com/consul/1.0.6/consul_1.0.6_windows_amd64.zip"
$arangodb = "https://download.arangodb.com/arangodb33/Windows7/x86_64/ArangoDB3-3.3.4-1_win64.zip"
$nomad = "https://releases.hashicorp.com/nomad/0.7.1/nomad_0.7.1_windows_amd64.zip"
$minio = "https://dl.minio.io/server/minio/release/windows-amd64/minio.exe"
$fabio = "https://github.com/fabiolb/fabio/releases/download/v1.5.8/fabio-1.5.8-go1.10-windows_amd64.exe"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function download ($url, $path) {
  if(!(Test-Path $path)){
    Invoke-WebRequest -Uri $url -OutFile $path
  }
}

function unzip ($origin, $destination) {
  Expand-Archive $origin -DestinationPath $destination
}

function zip ($origin, $destination) {
  Compress-Archive $origin $destination
}


download $consul ./bin/consul.zip
download $arangodb ./bin/arangodb.zip
download $nomad ./bin/nomad.zip
download $minio ./bin/minio.exe
download $fabio ./bin/fabio.exe

if(!(Test-Path ./bin/consul.exe)){
  unzip ./bin/consul.zip ./bin
}

if(!(Test-Path ./bin/nomad.exe)){
  unzip ./bin/nomad.zip ./bin
}

if(!(Test-Path ./bin/ArangoDB3-3.3.4-1_win64/usr/bin/arangodb.exe)){
  unzip ./bin/arangodb.zip ./bin
}

$consulbin = './bin/consul.exe'
$nomadbin = './bin/nomad.exe'
$arangobin = './bin/ArangoDB3-3.3.4-1_win64/usr/bin/arangod.exe'
$miniobin = './bin/minio.exe'

mkdir .\data
mkdir data/nomad
mkdir data/consul
mkdir data/nomad/config

cp ./nomad.client.hcl ./data/nomad/config/nomad.client.hcl

if($minioJob.State -ne 'Running'){
  $miniobin = start-job {
    param($miniobin)
    set-alias miniobin $miniobin
    ./bin/minio.exe server ./data/minio
  }  -ArgumentList $miniobin -Init ([ScriptBlock]::Create("Set-Location '$pwd'"))
}

if($consuljob.State -ne 'Running'){
  $consuljob = start-job {
    param($consulbin)
    set-alias consulbin $consulbin
    $datadir = resolve-path ./data/consul
    ./bin/consul.exe agent -dev -data-dir $datadir
  }  -ArgumentList $consulbin -Init ([ScriptBlock]::Create("Set-Location '$pwd'"))
}

if($nomadjob.State -ne 'Running'){
  $nomadjob = start-job {
    param($nomadbin)
    set-alias nomadbin $nomadbin
    $datadir = resolve-path ./data/nomad
    $configdir = Resolve-Path ./data/nomad/config
    nomadbin agent -dev -data-dir $datadir -config $configdir
  } -ArgumentList $nomadbin -Init ([ScriptBlock]::Create("Set-Location '$pwd'"))
}

if($arangodbjob.State -ne 'Running'){
  $arangodbjob = start-job {
    param($arangobin)
    set-alias arangobin $arangobin
    arangobin --database.directory ./data/arangodb 
  } -ArgumentList $arangobin -Init ([ScriptBlock]::Create("Set-Location '$pwd'"))
}

nomad run ./fabio.nomad

dotnet build .\src\silo
$outPath = (resolve-path .\out)
dotnet publish -o $outPath .\src\silo

$serial = [System.DateTime]::Now.ToString("ddHHmmss")

zip ./out/* out.$serial.zip

mc cp out.$serial.zip myminio/nomad
rm out.$serial.zip


dotnet build .\src\front
mkdir front
$frontPath = (resolve-path .\front)
dotnet publish -o $frontPath .\src\front

zip ./front/* front.$serial.zip

mc cp front.$serial.zip myminio/nomad
rm front.$serial.zip


(Get-Content ".\test.nomad.template").Replace('${{DEPLOYMENT_FILE}}', "out.$serial.zip") | Set-Content ./test.nomad
(Get-Content ".\front.nomad.template").Replace('${{DEPLOYMENT_FILE}}', "front.$serial.zip") | Set-Content ./front.nomad


nomad run ./test.nomad
nomad run ./front.nomad