# Local AI Video — Native C# + WPF

Bu klasör, tarayıcı tabanlı sürümün **tamamen native Windows (C# + WPF)** karşıılığıdır.
HTML, Electron, WebView veya herhangi bir tarayıcı arayüzü **kullanılmaz**. Arayüz WPF/
XAML ile, üretim motoru ise saf .NET + FFmpeg ile yazılmıştır.

## Neden native?
- Arayüz: WPF (XAML) + WPF DrawingContext ile prosedürel 2D sahne motoru.
- LLM: **Ollama (localhost REST)** — `http://127.0.0.1:11434`.
- TTS: **System.Speech (SAPI)** — Windows'a gömülü, çevrimdışı, harici servis yok.
- Ses karışımı: saf .NET (WAV mix + voice ducking), harici audio kütüphanesi yok.
- Video kodlama: **FFmpeg** (rawvideo → H.264 + AAC MP4). Ekran kaydı değil, gerçek frame kodlama.

## Gereksinimler
- **Windows 10/11**, **.NET 8 SDK**
- **Ollama** (yerel LLM): https://ollama.com  →  `ollama pull qwen2.5:0.5b`
- **FFmpeg** (video kodlama): `ffmpeg.exe`'yi `bin/` klasörüne koy veya `PATH`'e ekle.
  (Uygulama önce uygulama klasöründeki `ffmpeg.exe`'yi, sonra PATH'i arar.)

## Derleme
```
cd wpf
dotnet build -c Release
# veya Visual Studio ile LocalAIVideo.sln aç → Build
```

## Çalıştırma
```
dotnet run -c Release
```
1. Ollama'nın çalıştığından emin ol (`ollama serve`).
2. Konu yaz, süre/oran/dil/kalite seç.
3. **GENERATE VIDEO**'ya bas.
4. Çıktı: `%APPDATA%\LocalAIVideo\projects\<id>\export\final.mp4`

## Proje yapısı
```
wpf/src/LocalAIVideo/
  App.xaml                koyu tema + stiller + converter'lar
  MainWindow.xaml         3 sütunlu stüdyo arayüzü
  Models/                 veri modelleri (Project, Scene, SceneSpec, StyleBible…)
  Engine/                 prosedürel 2D sahne motoru
    Palette.cs            stil / ışık / renk / grain
    Camera.cs             shot + hareket + parallax matrisi
    CharacterRenderer.cs  parametrik karakter (insan/animal/robot/gölge/abstract)
    EnvironmentRenderer.cs oda / sokak / doğa / uzay + objeler
    EffectsRenderer.cs    yağmur / kar / sis / toz / şimşek / parçacık…
    SceneRenderer.cs      katmanlı kompozisyon (FAR→…→FOREGROUND) + ışık
  Media/                  TtsService, WavFile, AudioMixer, VideoEncoder(FFmpeg)
  Core/                   ScriptEngine(Ollama), Pipeline, Storage, Hardware
  ViewModels/             MainViewModel (MVVM), ObservableObject, RelayCommand
  Helpers/                Json, ObservableObject
```

## Dürüst sınırlamalar
- Bu çözüm **bu Linux web kumandasında derlenemez** (WPF yalnızca Windows'ta derlenir).
  Kod, Windows'ta `dotnet build` ile derlenmek üzere yazılmıştır.
- LLM için Ollama, video için FFmpeg gerekir; bunlar yoksa uygulama açıkça uyarır,
  sahte sonuç üretmez.
- TTS, Windows'un yerleşik SAPI seslerini kullanır (Kokoro'nun native karşılığı olarak).
  Kokoro'nun tam nitelikli native C# portu (ONNX) eklenirse `Media/TtsService`
  arayüzü aynı kalır, yalnızca bir başka sağlayıcı eklenir.
