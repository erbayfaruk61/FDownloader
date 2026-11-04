# FDownloader — Windows Kod İmzalama (Code Signing) ve Güven Rehberi

> **Amaç:** FDownloader yürütülebilir dosyalarının (`Setup.exe`, `FDownloader.App.exe`) Windows SmartScreen uyarılarını aşmasını sağlamak, geliştirici kimliğini doğrulamak ve güvenilir bir dağıtım hattı kurmak.

---

## 1. Kod İmzalama Seçenekleri

| Yöntem | Maliyet | SmartScreen İtibarı | CI/CD Entegrasyonu |
|---|---|---|---|
| **Açık Kaynak Proje Destekleri (SignPath.io vb.)** | Ücretsiz (Açık kaynak onayına bağlı) | Çok Yüksek (Hızlı itibar kazanımı) | GitHub Actions ile doğrudan uyumlu |
| **Bulut Tabanlı EV Kod İmzalama (Azure Trusted Signing)** | Aylık/İşlem Başı Düşük Maliyet | Anında Sıfır SmartScreen Uyarısı | `azure/trusted-signing-action` ile tam uyumlu |
| **Geleneksel OV Kod İmzalama (Sertifika Dosyası/Token)** | Yıllık ~\$150–\$300 | İndirme sayısına bağlı zamanla itibar | CI'da PFX gizli değişkeni (base64) veya HSM token |
| **Öz İmzalı (Self-Signed / Test İmzası)** | Ücretsiz | Yalnızca geliştirme/test ortamı | Test amaçlı |

---

## 2. GitHub Actions CI Kod İmzalama Entegrasyon Mimarisi

Sertifika veya anahtar dosyaları **asla kaynak kod deposunda saklanmaz**. Yalnızca GitHub Repository Secrets alanında tutulur.

### 2.1 Azure Trusted Signing (Önerilen)
GitHub Actions iş akışına eklenecek yapı:
```yaml
- name: Azure Trusted Signing ile İmzala
  if: env.AZURE_SIGNING_CONFIG != ''
  uses: azure/trusted-signing-action@v0.4.1
  with:
    azure-tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    azure-client-id: ${{ secrets.AZURE_CLIENT_ID }}
    azure-client-secret: ${{ secrets.AZURE_CLIENT_SECRET }}
    endpoint: https://eus.codesigning.azure.net/
    trusted-signing-account-name: ${{ secrets.SIGNING_ACCOUNT }}
    certificate-profile-name: ${{ secrets.CERT_PROFILE }}
    files: |
      publish/FDownloader.App.exe
```

### 2.2 PFX / SignTool (Geleneksel OV Sertifikaları)
```yaml
- name: SignTool ile İmzala
  if: env.CERT_BASE64 != ''
  shell: pwsh
  run: |
    $pfxBytes = [System.Convert]::FromBase64String("${{ secrets.CODE_SIGN_CERT_BASE64 }}")
    [System.IO.File]::WriteAllBytes("cert.pfx", $pfxBytes)
    & "signtool.exe" sign /f cert.pfx /p "${{ secrets.CODE_SIGN_PASSWORD }}" /tr http://timestamp.digicert.com /td sha256 /fd sha256 ./publish/FDownloader.App.exe
    Remove-Item cert.pfx -Force
```

### 2.3 Velopack ile Entegre İmzalama
Velopack `vpk pack` komutu, `--signParams` veya `--signTemplate` parametresiyle hem iç ana `.exe`yi hem de üretilen `Setup.exe`yi tek seferde otomatik imzalar:
```powershell
vpk pack `
  --packId FDownloader `
  --packVersion $version `
  --packDir ./publish `
  --mainExe FDownloader.App.exe `
  --signParams "/f cert.pfx /p secret /tr http://timestamp.digicert.com /td sha256 /fd sha256"
```

