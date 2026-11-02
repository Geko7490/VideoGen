import { useState } from "react";

/**
 * This served page is a build/overview guide for the NATIVE WPF solution.
 * The actual application is a C# + WPF project (see /wpf). No browser UI is used by the app itself.
 */

const MAPPING = [
  { web: "WebLLM / WebGPU", native: "Ollama (localhost REST)", note: "Yerel LLM — localhost:11434" },
  { web: "Kokoro ONNX (tarayıcı)", native: "System.Speech (SAPI)", note: "Windows'a gömülü, çevrimdışı TTS" },
  { web: "Canvas 2D procedural engine", native: "WPF DrawingContext engine", note: "Aynı sahne motoru, native" },
  { web: "WebCodecs (H.264/VP9)", native: "FFmpeg (H.264 + AAC MP4)", note: "Gerçek frame kodlama" },
  { web: "OfflineAudioContext mix", native: "Saf .NET WAV mixer", note: "Voice ducking dahil" },
  { web: "IndexedDB", native: "%APPDATA% + JSON + dosya", note: "Kalıcı proje depolama" },
  { web: "React arayüz", native: "WPF / XAML arayüz", note: "Aynı koyu tasarım dili" },
];

const STEPS = [
  "Konu yaz → yerel LLM (Ollama) hikâye + sahne planı üretir",
  "Her sahne için Scene JSON (ortam, ışık, karakter, kamera, efekt)",
  "Sıralı TTS (System.Speech) → gerçek ses süresi ölçülür",
  "Saf .NET ile ses karışımı (narration + müzik + ducking)",
  "Prosedürel 2D sahne motoru frame frame render eder",
  "FFmpeg ile H.264 + AAC MP4'e kodlar → final.mp4",
];

export default function App() {
  const [open, setOpen] = useState(true);
  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-200 font-[system-ui]">
      <div className="mx-auto max-w-4xl px-6 py-12">
        {/* header */}
        <div className="flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-500 text-xl font-black text-black shadow-lg shadow-emerald-500/20">▶</div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-white">Local AI Video</h1>
            <p className="text-sm text-zinc-400">Native C# + WPF Windows uygulaması</p>
          </div>
        </div>

        <div className="mt-6 rounded-xl border border-emerald-800/50 bg-emerald-950/20 p-4 text-sm text-emerald-200">
          <b>Önemli:</b> Bu proje artık <b>native Windows</b> uygulamasıdır. Kaynak kod <code className="rounded bg-black/40 px-1.5 py-0.5">/wpf</code>{" "}
          klasöründedir. Bu web sayfası yalnızca <b>derleme &amp; çalıştırma rehberi</b> olarak servis edilmektedir; uygulama kendisi HTML/WebView kullanmaz.
        </div>

        {/* build steps */}
        <section className="mt-10">
          <h2 className="mb-3 text-lg font-semibold text-white">Derle &amp; Çalıştır</h2>
          <div className="overflow-hidden rounded-xl border border-zinc-800 bg-[#0c0c0f]">
            <pre className="overflow-x-auto p-4 text-sm leading-relaxed text-emerald-300">
{`# Gereksinimler: .NET 8 SDK, Ollama, FFmpeg
ollama pull qwen2.5:0.5b

cd wpf
dotnet build -c Release
dotnet run -c Release
`}
            </pre>
          </div>
          <ul className="mt-4 grid gap-2 sm:grid-cols-3 text-sm text-zinc-400">
            <li className="rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3"><b className="text-zinc-200">.NET 8 SDK</b><br/>Windows için gerekli</li>
            <li className="rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3"><b className="text-zinc-200">Ollama</b><br/>Yerel LLM (localhost)</li>
            <li className="rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3"><b className="text-zinc-200">FFmpeg</b><br/>Video kodlama</li>
          </ul>
        </section>

        {/* pipeline */}
        <section className="mt-10">
          <h2 className="mb-3 text-lg font-semibold text-white">Üretim Hattı</h2>
          <ol className="space-y-2">
            {STEPS.map((s, i) => (
              <li key={i} className="flex items-start gap-3 rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-500/15 text-xs font-bold text-emerald-400">{i + 1}</span>
                <span className="text-zinc-300">{s}</span>
              </li>
            ))}
          </ol>
        </section>

        {/* mapping */}
        <section className="mt-10">
          <h2 className="mb-3 text-lg font-semibold text-white">Teknoloji Eşlemesi</h2>
          <div className="overflow-hidden rounded-xl border border-zinc-800">
            <table className="w-full text-sm">
              <thead className="bg-[#0c0c0f] text-left text-xs uppercase tracking-wider text-zinc-500">
                <tr>
                  <th className="p-3">Tarayıcı (eski)</th>
                  <th className="p-3">Native (yeni)</th>
                  <th className="p-3">Not</th>
                </tr>
              </thead>
              <tbody>
                {MAPPING.map((m, i) => (
                  <tr key={i} className="border-t border-zinc-800">
                    <td className="p-3 text-zinc-400">{m.web}</td>
                    <td className="p-3 font-medium text-emerald-300">{m.native}</td>
                    <td className="p-3 text-zinc-500">{m.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        {/* project tree */}
        <section className="mt-10">
          <button onClick={() => setOpen(!open)} className="mb-3 flex w-full items-center justify-between text-lg font-semibold text-white">
            <span>Proje Yapısı</span>
            <span className="text-zinc-500">{open ? "▾" : "▸"}</span>
          </button>
          {open && (
            <pre className="overflow-x-auto rounded-xl border border-zinc-800 bg-[#0c0c0f] p-4 text-xs leading-relaxed text-zinc-300">
{`wpf/src/LocalAIVideo/
  App.xaml                 koyu tema + stiller
  MainWindow.xaml          3 sütunlu stüdyo arayüzü
  Models/                  Project · Scene · SceneSpec · StyleBible
  Engine/                  Palette · Camera · CharacterRenderer
                           EnvironmentRenderer · EffectsRenderer · SceneRenderer
  Media/                   TtsService · WavFile · AudioMixer · VideoEncoder(FFmpeg)
  Core/                    ScriptEngine(Ollama) · Pipeline · Storage · Hardware
  ViewModels/              MainViewModel (MVVM) · RelayCommand
  Helpers/                 Json · ObservableObject`}
            </pre>
          )}
        </section>

        {/* honest limits */}
        <section className="mt-10">
          <h2 className="mb-3 text-lg font-semibold text-white">Dürüst Sınırlamalar</h2>
          <ul className="space-y-2 text-sm text-zinc-400">
            <li className="rounded-lg border border-amber-900/40 bg-amber-950/15 p-3">
              Bu çözüm <b>Windows</b>'ta <code className="rounded bg-black/40 px-1">dotnet build</code> ile derlenir; WPF Linux'ta derlenemez.
            </li>
            <li className="rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3">
              LLM için <b>Ollama</b>, video için <b>FFmpeg</b> gerekir. Yoksa uygulama açıkça uyarır, sahte sonuç üretmez.
            </li>
            <li className="rounded-lg border border-zinc-800 bg-[#0c0c0f] p-3">
              TTS, Windows'un yerleşik <b>SAPI</b> seslerini kullanır. Kokoro'nun native C# (ONNX) portu eklenirse{" "}
              <code className="rounded bg-black/40 px-1">Media/TtsService</code> arayüzü aynı kalır.
            </li>
          </ul>
        </section>

        <div className="mt-12 border-t border-zinc-800 pt-6 text-xs text-zinc-600">
          Local AI Video · %100 yerel / çevrimdışı · HİÇBİR veriniz buluta gitmez.
        </div>
      </div>
    </div>
  );
}
