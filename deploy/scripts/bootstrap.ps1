<#
.SYNOPSIS
  Lokal Kalamar cluster'ını kurar: k3d cluster + Argo CD + root (app-of-apps) Application.

.DESCRIPTION
  Tekrar çalıştırılabilir; var olan adımlar atlanır.
    1. Araçları kontrol eder (docker, k3d, kubectl, helm).
    2. k3d cluster'ını oluşturur (deploy/k3d/cluster.yaml) veya başlatır.
    3. Sealed Secrets anahtarının yedeği varsa cluster'a geri yükler.
    4. Argo CD'yi Helm ile kurar/günceller (deploy/argocd/argocd-values.yaml).
    5. root Application'ı uygular; Argo CD gerisini (sealed-secrets, kalamar-dev) Git'ten kurar.
    6. Sealed Secrets anahtarını yedekler (yedek yoksa).
    7. Argo CD giriş bilgilerini ve adresleri yazdırır.

.EXAMPLE
  .\deploy\scripts\bootstrap.ps1
#>
[CmdletBinding()]
param(
    # Sealed Secrets private key yedeği. Bu dosya Git'e GİRMEMELİ: Git'teki tüm SealedSecret'ları çözebilir.
    [string]$KeyBackupPath = (Join-Path $env:USERPROFILE '.kalamar\sealed-secrets-key.yaml'),
    # sealed-secrets controller'ının hazır olması için beklenecek süre (sn).
    [int]$SealedSecretsWaitSeconds = 300
)

$ErrorActionPreference = 'Stop'

$ClusterName = 'kalamar'
$Context = "k3d-$ClusterName"
$ArgoCdChartVersion = '10.18.3'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ClusterConfig = Join-Path $RepoRoot 'deploy\k3d\cluster.yaml'
$ArgoCdValues = Join-Path $RepoRoot 'deploy\argocd\argocd-values.yaml'
$RootApp = Join-Path $RepoRoot 'deploy\argocd\root-app.yaml'
$Environments = @('dev', 'prod')
$SealedKeyLabel = 'sealedsecrets.bitnami.com/sealed-secrets-key'

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Native komutu çalıştırır; çıkış kodu 0 değilse durur.
function Invoke-Checked([string]$File, [string[]]$ArgumentList) {
    & $File @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Komut başarısız oldu (çıkış kodu $LASTEXITCODE): $File $($ArgumentList -join ' ')"
    }
}

# Native komutu sessizce çalıştırır; başarılıysa $true döner.
function Test-Native([string]$File, [string[]]$ArgumentList) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $File @ArgumentList *> $null
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

# Native komutun stdout'unu döndürür; hata olursa boş döner.
function Get-NativeOutput([string]$File, [string[]]$ArgumentList) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $File @ArgumentList 2> $null
        if ($LASTEXITCODE -ne 0) { return '' }
        return (($output | Out-String).Trim())
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

# --- 1. Araçlar -------------------------------------------------------------------------------
Write-Step 'Araçlar kontrol ediliyor'
$tools = [ordered]@{
    docker  = 'Docker Desktop: https://www.docker.com/products/docker-desktop/'
    k3d     = 'winget install k3d.k3d'
    kubectl = 'winget install Kubernetes.kubectl'
    helm    = 'winget install Helm.Helm'
}
$missing = @()
foreach ($tool in $tools.Keys) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        $missing += "  $tool  ->  $($tools[$tool])"
    }
}
if ($missing.Count -gt 0) {
    Write-Host 'Eksik araçlar var; kurup yeni bir terminalde tekrar çalıştırın:' -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host $_ }
    exit 1
}
if (-not (Test-Native 'docker' @('info'))) {
    throw 'Docker çalışmıyor. Docker Desktop''ı başlatıp tekrar deneyin.'
}
Write-Host 'docker, k3d, kubectl, helm: tamam'

# --- 2. Cluster -------------------------------------------------------------------------------
if (Test-Native 'k3d' @('cluster', 'get', $ClusterName)) {
    Write-Step "k3d cluster '$ClusterName' zaten var; başlatılıyor"
    Invoke-Checked 'k3d' @('cluster', 'start', $ClusterName)
}
else {
    Write-Step "k3d cluster '$ClusterName' oluşturuluyor (ilk seferde image indirmesi birkaç dakika sürebilir)"
    Invoke-Checked 'k3d' @('cluster', 'create', '--config', $ClusterConfig)
}
Invoke-Checked 'kubectl' @('config', 'use-context', $Context)
Invoke-Checked 'kubectl' @('wait', '--for=condition=Ready', 'nodes', '--all', '--timeout=180s')

# --- 3. Sealed Secrets anahtarını geri yükle -------------------------------------------------
$existingKey = Get-NativeOutput 'kubectl' @('get', 'secret', '-n', 'kube-system', '-l', $SealedKeyLabel, '-o', 'name')
if (-not $existingKey -and (Test-Path $KeyBackupPath)) {
    Write-Step "Sealed Secrets anahtarı yedekten geri yükleniyor: $KeyBackupPath"
    Invoke-Checked 'kubectl' @('apply', '-n', 'kube-system', '-f', $KeyBackupPath)
    # Controller zaten çalışıyorsa yeni anahtarı okuması için yeniden başlatılır.
    if (Test-Native 'kubectl' @('get', 'deploy', 'sealed-secrets-controller', '-n', 'kube-system')) {
        Invoke-Checked 'kubectl' @('rollout', 'restart', 'deploy/sealed-secrets-controller', '-n', 'kube-system')
    }
}

# --- 4. Argo CD -------------------------------------------------------------------------------
Write-Step "Argo CD kuruluyor/güncelleniyor (argo-cd chart $ArgoCdChartVersion)"
Invoke-Checked 'helm' @('repo', 'add', 'argo', 'https://argoproj.github.io/argo-helm', '--force-update')
Invoke-Checked 'helm' @('upgrade', '--install', 'argocd', 'argo/argo-cd',
    '--version', $ArgoCdChartVersion,
    '--namespace', 'argocd', '--create-namespace',
    '--values', $ArgoCdValues,
    '--wait', '--timeout', '10m')

# --- 5. Root Application ----------------------------------------------------------------------
Write-Step 'root Application uygulanıyor (Argo CD deploy/argocd/apps/ klasörünü main''den okur)'
Invoke-Checked 'kubectl' @('apply', '-f', $RootApp)

# --- 6. Sealed Secrets anahtarını yedekle -----------------------------------------------------
Write-Step "sealed-secrets controller'ı bekleniyor (en fazla $SealedSecretsWaitSeconds sn)"
$deadline = (Get-Date).AddSeconds($SealedSecretsWaitSeconds)
$controllerReady = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Native 'kubectl' @('get', 'deploy', 'sealed-secrets-controller', '-n', 'kube-system')) {
        $controllerReady = Test-Native 'kubectl' @('rollout', 'status', 'deploy/sealed-secrets-controller', '-n', 'kube-system', '--timeout=120s')
        if ($controllerReady) { break }
    }
    Start-Sleep -Seconds 10
}

if (-not $controllerReady) {
    Write-Warning ('sealed-secrets controller hazır değil. Argo CD UI''da root ve sealed-secrets uygulamalarını kontrol edin; ' +
        'deploy/argocd/apps/ main''de yoksa root uygulaması onu bulamaz. Hazır olunca bu scripti tekrar çalıştırın.')
}
elseif (-not (Test-Path $KeyBackupPath)) {
    $keyYaml = Get-NativeOutput 'kubectl' @('get', 'secret', '-n', 'kube-system', '-l', $SealedKeyLabel, '-o', 'yaml')
    if ($keyYaml -cmatch 'kind: Secret') {
        # Yeni bir cluster'a uygulanabilmesi için cluster'a özgü alanlar çıkarılır.
        $keyYaml = $keyYaml -creplace '(?m)^[ \t]*(resourceVersion|uid|creationTimestamp):[^\r\n]*(\r?\n|$)', ''
        New-Item -ItemType Directory -Force -Path (Split-Path $KeyBackupPath) | Out-Null
        [IO.File]::WriteAllText($KeyBackupPath, $keyYaml + "`n", (New-Object Text.UTF8Encoding $false))
        Write-Step "Sealed Secrets anahtarı yedeklendi: $KeyBackupPath"
        Write-Host 'Bu dosya bir private key''dir: Git''e eklemeyin, paylaşmayın. Cluster silinip yeniden kurulursa' -ForegroundColor Yellow
        Write-Host 'bootstrap bu yedeği geri yükler; böylece Git''teki sealed değerler geçerli kalır.' -ForegroundColor Yellow
    }
}

# --- 7. Bilgiler ------------------------------------------------------------------------------
$encoded = Get-NativeOutput 'kubectl' @('get', 'secret', 'argocd-initial-admin-secret', '-n', 'argocd', '-o', 'jsonpath={.data.password}')
$adminPassword = if ($encoded) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded)) } else { '(argocd-initial-admin-secret bulunamadı)' }

Write-Step 'Hazır'
Write-Host "  Argo CD UI : http://argocd.localhost:8000   (kullanıcı: admin, şifre: $adminPassword)"
Write-Host '  Dev        : http://kalamar.localhost:8000        (her main build''i)'
Write-Host '  Prod       : http://kalamar-prod.localhost:8000   (yalnızca vX.Y.Z tag''leri)'
Write-Host '  Durum      : kubectl get applications -n argocd'
Write-Host '               kubectl get pods -n kalamar-dev'

foreach ($envName in $Environments) {
    $valuesFile = Join-Path $RepoRoot "helm\kalamar\values-$envName.yaml"
    $valuesContent = [IO.File]::ReadAllText($valuesFile)
    if ($valuesContent -cmatch '(?m)^\s*sealedSaPassword:\s*""\s*$') {
        Write-Host ''
        Write-Host "values-$envName.yaml içinde sealedSaPassword boş; kalamar-$envName uygulaması şifre kaynağı olmadan render edilemez." -ForegroundColor Yellow
        Write-Host "Sıradaki adım: .\deploy\scripts\seal-sqlserver-secret.ps1 -Environment $envName, ardından değişikliği commit edip main'e push edin." -ForegroundColor Yellow
    }
}
