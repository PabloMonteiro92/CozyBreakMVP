param(
  [Parameter(Mandatory=$true)][string]$BinaryPath,
  [Parameter(Mandatory=$true)][string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$certificate = Get-ChildItem Cert:\CurrentUser\My\$CertificateThumbprint
if ($null -eq $certificate) { throw "Certificado não encontrado no perfil CurrentUser\My." }
if (-not $certificate.HasPrivateKey) { throw "O certificado não possui chave privada." }

Set-AuthenticodeSignature -FilePath $BinaryPath -Certificate $certificate -TimestampServer 'http://timestamp.digicert.com' | Format-List
$sig = Get-AuthenticodeSignature -FilePath $BinaryPath
if ($sig.Status -ne 'Valid') { throw "Assinatura inválida: $($sig.Status)" }
