# Katkıda Bulunma Rehberi (Contributing Guidelines)

FDownloader açık kaynaklı bir topluluk projesidir. Hata düzeltmeleri, yeni özellikler, dokümantasyon iyileştirmeleri ve test katkıları memnuniyetle karşılanır.

---

## 1. Geliştirme Ortamı Kurulumu

- **SDK:** .NET 9.0 SDK veya üzeri
- **IDE:** Visual Studio 2022 (v17.12+), JetBrains Rider veya VS Code (C# Dev Kit eklentisiyle)
- **Çalıştırma:**
  ```powershell
  dotnet restore
  dotnet build
  dotnet test
  ```

---

## 2. Kodlama Standartları

1. **Temiz Kod & Minimalizm:** Kod sade, doğrudan ve gereksiz soyutlamalardan arındırılmış olmalıdır.
2. **Sabitler ve Kod Kalitesi:** Sihirli sayılar (magic numbers) ve tekrarlanan metinler yerine sabit/enum tanımları tercih edilmelidir.
3. **Güvenlik İlkeleri:** Hassas veriler (şifreler, belirteçler) şifresiz düz metin saklanamaz. Harici girdiler daima sanitize edilmelidir.
4. **Birim Testleri:** Eklenen her yeni işlevsellik veya düzeltilen hata için `tests/FDownloader.Core.Tests` altına birim testi yazılmalı, `dotnet test` tümüyle yeşil olmalıdır.

---

## 3. Katkı ve PR (Pull Request) Akışı

1. Depoyu forklayın ve ilgili özellik için yeni bir dal (branch) açın:
   ```bash
   git checkout -b feature/yeni-ozellik
   ```
2. Değişikliklerinizi yapın ve testlerin geçtiğini doğrulayın:
   ```bash
   dotnet test
   ```
3. Commit mesajlarında [Conventional Commits](https://www.conventionalcommits.org/) standardını kullanın:
   - `feat(module): Yeni özellik açıklaması`
   - `fix(module): Hata düzeltmesi`
   - `docs(module): Dokümantasyon güncellemesi`
4. Pull Request açarken değişikliklerinizi ve test sonuçlarını özetleyin.

