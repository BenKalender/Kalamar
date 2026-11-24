<#
.SYNOPSIS
  SQL Server SA şifresini Sealed Secrets ile şifreleyip helm/kalamar/values-dev.yaml'a yazar.

.DESCRIPTION
  Şifre yalnızca bu cluster'daki sealed-secrets controller'ının anahtarıyla çözülebilecek şekilde,
  "<ReleaseName>-sqlserver" adı ve <Namespace> namespace'i için (strict scope) şifrelenir.
  Sonuç Git'e girmesi güvenli bir değerdir; düz metin şifre diske kalıcı olarak yazılmaz.
  Ardından değişikliği commit edip main'e push edin; Argo CD SealedSecret'ı oluşturur.

.EXAMPLE
  .\deploy\scripts\seal-sqlserver-secret.ps1
#>
[CmdletBinding()]
param(
    [string]$Namespace = 'kalamar-dev',
    # Argo CD Application'daki helm.releaseName; SealedSecret adı "<ReleaseName>-sqlserver" olur.
    [string]$ReleaseName = 'kalamar-dev',
    [string]$ValuesFile = (Join-Path $PSScriptRoot '..\..\helm\kalamar\values-dev.yaml'),
    # Verilmezse şifre iki kez sorulur.
    [SecureString]$Password
)

$ErrorActionPreference = 'Stop'

$ExpectedContext = 'k3d-kalamar'
$SecretName = "$ReleaseName-sqlserver"
$KubesealUrl = 'https://github.com/bitnami/sealed-secrets/releases (kubeseal-<sürüm>-windows-amd64.tar.gz)'

function ConvertTo-PlainText([SecureString]$Secure) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# SQL Server şifre politikası + connection string kısıtı. Sorun yoksa $null döner.
function Get-PasswordProblem([string]$Plain) {
    if ($Plain.Length -lt 8) { return 'en az 8 karakter olmalı' }
    if ($Plain.Length -gt 128) { return 'en fazla 128 karakter olabilir' }
    if ($Plain.Contains(';')) { return "';' içeremez (connection string'i bozar)" }
    $classes = 0
    if ($Plain -cmatch '[A-Z]') { $classes++ }
    if ($Plain -cmatch '[a-z]') { $classes++ }
    if ($Plain -cmatch '[0-9]') { $classes++ }
    if ($Plain -cmatch '[^A-Za-z0-9]') { $classes++ }
    if ($classes -lt 3) { return 'büyük harf, küçük harf, rakam ve sembol türlerinden en az 3''ünü içermeli' }
    return $null
}

# --- Ön kontroller ----------------------------------------------------------------------------
foreach ($tool in @('kubectl', 'kubeseal')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        if ($tool -eq 'kubeseal') { throw "kubeseal bulunamadı. İndirip PATH'e ekleyin: $KubesealUrl" }
        throw "$tool bulunamadı."
    }
}
$ValuesFile = (Resolve-Path $ValuesFile).Path

$currentContext = (& kubectl config current-context | Out-String).Trim()
if ($currentContext -ne $ExpectedContext) {
    Write-Warning "Aktif kubectl context '$currentContext' (beklenen: '$ExpectedContext'). Değer bu cluster'ın anahtarıyla şifrelenecek."
}

# --- Şifre ------------------------------------------------------------------------------------
if ($Password) {
    $plain = ConvertTo-PlainText $Password
    $problem = Get-PasswordProblem $plain
    if ($problem) { throw "Şifre uygun değil: $problem" }
}
else {
    while ($true) {
        $plain = ConvertTo-PlainText (Read-Host -AsSecureString 'SQL Server SA şifresi')
        $problem = Get-PasswordProblem $plain
        if ($problem) { Write-Host "Şifre uygun değil: $problem" -ForegroundColor Yellow; continue }
        $again = ConvertTo-PlainText (Read-Host -AsSecureString 'Şifre (tekrar)')
        if ($again -cne $plain) { Write-Host 'Şifreler eşleşmiyor.' -ForegroundColor Yellow; continue }
        break
    }
}

# --- Şifreleme --------------------------------------------------------------------------------
# PowerShell 5.1 native komutlara pipe'lanan metnin sonuna satır sonu ekler; şifreye karışmaması için
# değer satır sonu olmadan geçici bir dosyaya yazılıp kubeseal'a dosya olarak verilir.
$tmp = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($tmp, $plain, (New-Object Text.UTF8Encoding $false))
    $output = & kubeseal --raw --scope strict `
        --namespace $Namespace --name $SecretName `
        --controller-namespace kube-system --controller-name sealed-secrets-controller `
        "--from-file=$tmp"
    if ($LASTEXITCODE -ne 0) { throw "kubeseal başarısız oldu (çıkış kodu $LASTEXITCODE). sealed-secrets controller çalışıyor mu?" }
}
finally {
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    $plain = $null
    $again = $null
}

$sealed = (($output | Out-String) -creplace '\s', '')
if ($sealed -cnotmatch '^[A-Za-z0-9+/=]+$') { throw 'kubeseal beklenmeyen bir çıktı üretti.' }

# --- values-dev.yaml --------------------------------------------------------------------------
$content = [IO.File]::ReadAllText($ValuesFile)
$pattern = '(?m)^(?<key>[ \t]*sealedSaPassword:)[^\r\n]*'
$matchCount = ([regex]::Matches($content, $pattern)).Count
if ($matchCount -ne 1) { throw "$ValuesFile içinde tek bir 'sealedSaPassword:' satırı bekleniyordu ($matchCount bulundu)." }
$content = [regex]::Replace($content, $pattern, { param($m) $m.Groups['key'].Value + ' "' + $sealed + '"' })
[IO.File]::WriteAllText($ValuesFile, $content, (New-Object Text.UTF8Encoding $false))

Write-Host ''
Write-Host "Şifrelenmiş değer yazıldı: $ValuesFile" -ForegroundColor Green
Write-Host "  (SealedSecret: $Namespace/$SecretName; yalnızca bu cluster'ın anahtarıyla çözülebilir)"
Write-Host ''
Write-Host 'Sıradaki adım: değişikliği commit edip main''e push edin; Argo CD SealedSecret''ı ve SQL Server''ı kurar.'
Write-Host '  git add helm/kalamar/values-dev.yaml'
Write-Host '  git commit -m "Seal SQL Server SA password for kalamar-dev"'
