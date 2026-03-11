## Mandelbrot Demo

Zadanie z predmetu Parallel Computer Systems zamerané na porovnanie klasického jednovláknového výpočtu Mandelbrotovej množiny s paralelnou verziou implementovanou pomocou `Parallel.For`.

## Navigacia

- [Prehľad](#prehlad)
- [Vizualna galeria](#vizualna-galeria)
- [Zhrnutie vysledkov](#zhrnutie-vysledkov)
- [Grafy](#grafy)
- [Zrychlenie a efektivita](#zrychlenie-a-efektivita)
- [Profilovanie v AMD uProf](#profilovanie-v-amd-uprof)
- [Ako to funguje](#ako-to-funguje)
- [Ukazky kodu](#ukazky-kodu)
- [Spustenie](#spustenie)
- [Vstupy](#vstupy)
- [Vystupy](#vystupy)
- [Struktura projektu](#struktura-projektu)
- [Konfiguracia](#konfiguracia)
- [Nastavenia projektu](#nastavenia-projektu)
- [Testovacie prostredie](#testovacie-prostredie)

<a id="prehlad"></a>
## Prehlad

Tento projekt počíta a vizualizuje Mandelbrotovu množinu, meria čas vykonania, zapisuje benchmark reporty a zobrazuje výsledný obrázok vo Windows Forms okne.

Hlavné porovnanie v tomto repozitári je:

- **Bežný výpočet**: jedno vlákno spracuje všetky riadky a všetky pixely pomocou štandardného vnoreného cyklu.
- **Paralelný výpočet pomocou `Parallel.For`**: riadky sú rozdelené medzi viaceré pracovné vlákna, pričom matematický výpočet zostáva úplne rovnaký.

To znamená, že **výsledný obrázok je identický**, ale celkový čas behu môže byť pri dostatočne veľkej úlohe výrazne nižší.

<a id="vizualna-galeria"></a>
## Vizualna galeria

Repozitár obsahuje kurátorský priečinok `images/` pre náhľady v README. Kompletné vygenerované rendery sa ukladajú do `mandelbrot-images/`, ktorý je ignorovaný v Gite.

### Hlbka iteracii pri 800x600

<table>
  <tr>
    <td align="center" width="50%">
      <img src="images/800x600-iterations-500.png" alt="Render Mandelbrotovej množiny v 800x600 s 500 iteráciami" width="100%" />
      <br />
      <sub>500 iterácií. Pri tejto hĺbke je tvar už dobre rozpoznateľný, ale hranica je ešte pomerne hrubá. Medián času: 117 ms sekvenčne, 16 ms pri 16 vláknach.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/800x600-iterations-5000.png" alt="Render Mandelbrotovej množiny v 800x600 s 5000 iteráciami" width="100%" />
      <br />
      <sub>5000 iterácií. Hrana je výrazne detailnejšia a stabilnejšia, ale množstvo práce rastie výrazne. Medián času: 1157 ms sekvenčne, 140 ms pri 16 vláknach.</sub>
    </td>
  </tr>
</table>

<a id="zhrnutie-vysledkov"></a>
## Zhrnutie vysledkov

Benchmark výsledky ukazujú dva jasné efekty:

1. Zvýšenie `maxIterations` zlepšuje vizuálny detail, ale zároveň zvyšuje čas výpočtu.
2. Nahradenie bežného vnoreného cyklu za `Parallel.For` zachová rovnaký obrázok, ale znižuje wall-clock čas.

Reprezentatívne mediány benchmarkov:

| Rozlisenie | Iteracie | Sekvencne | Paralelne 16 vlakien | Zrychlenie | Efektivita |
| --- | ---: | ---: | ---: | ---: | ---: |
| 800x600 | 500 | 117 ms | 16 ms | 7.31x | 45.70% |
| 800x600 | 5000 | 1157 ms | 140 ms | 8.26x | 51.62% |
| 1920x1080 | 1000 | 1362 ms | 135 ms | 10.09x | 63.06% |
| 1920x1080 | 5000 | 6714 ms | 696 ms | 9.65x | 60.29% |

Interpretácia:

- Menšie workloady z paralelizácie profitujú, ale stále je viditeľný overhead plánovania vlákien.
- Ťažšie workloady škálujú lepšie, pretože každé pracovné vlákno dostane dosť práce na to, aby sa amortizoval paralelný overhead.
- Implementácia nedosahuje ideálne lineárne škálovanie, čo je očakávané kvôli prenosom cez pamäť, overheadu plánovania a tomu, že nie každý pixel vyžaduje rovnaké množstvo práce.

<a id="grafy"></a>
## Grafy

Nasledujúce grafy sumarizujú namerané časy naprieč rozlíšeniami a počtami iterácií. Boli pripravené z benchmark dát a ukazujú rozdiel medzi bežnou implementáciou a implementáciou s `Parallel.For` pri rôznych počtoch vlákien.

<table>
  <tr>
    <td align="center" width="50%">
      <img src="images/800.png" alt="Benchmark graf pre rozlíšenie 800x600" width="100%" />
      <br />
      <sub>Porovnanie benchmarkov pre 800x600 naprieč počtami iterácií a vlákien.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/1920.png" alt="Benchmark graf pre rozlíšenie 1920x1080" width="100%" />
      <br />
      <sub>Porovnanie benchmarkov pre 1920x1080 naprieč počtami iterácií a vlákien.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <img src="images/2560.png" alt="Benchmark graf pre rozlíšenie 2560x1440" width="100%" />
      <br />
      <sub>Porovnanie benchmarkov pre 2560x1440 naprieč počtami iterácií a vlákien.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/4k.png" alt="Benchmark graf pre rozlíšenie 3840x2160" width="100%" />
      <br />
      <sub>Porovnanie benchmarkov pre 3840x2160 naprieč počtami iterácií a vlákien.</sub>
    </td>
  </tr>
</table>

<a id="zrychlenie-a-efektivita"></a>
## Zrychlenie a efektivita

Na vyhodnotenie škálovateľnosti projekt používa tieto štandardné metriky:

- `Speedup(p) = T_sekvencne / T_paralelne(p)`
- `Efficiency(p) = Speedup(p) / p`

Kde:

- `T_sekvencne` je medián času bežnej jednovláknovej implementácie
- `T_paralelne(p)` je medián času verzie s `Parallel.For` pri použití `p` vlákien

Príklad výpočtu pre `1920x1080`, `1000` iterácií a `16` vlákien:

- `T_sekvencne = 1362 ms`
- `T_paralelne(16) = 135 ms`
- `Speedup(16) = 1362 / 135 = 10.09x`
- `Efficiency(16) = 10.09 / 16 = 0.6306 = 63.06%`

To znamená, že paralelná verzia dokončí rovnakú prácu približne desaťkrát rýchlejšie, pričom každé vlákno prispieva asi 63 % ideálneho lineárneho zrýchlenia.

Nasledujúce grafy vizualizujú, ako sa zrýchlenie a efektivita správajú pri rôznych rozlíšeniach a workloadoch:

<table>
  <tr>
    <td align="center" width="50%">
      <img src="images/sp-800.png" alt="Graf zrýchlenia a efektivity pre 800x600" width="100%" />
      <br />
      <sub>Zrýchlenie a efektivita pre 800x600.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/sp-fhd.png" alt="Graf zrýchlenia a efektivity pre 1920x1080" width="100%" />
      <br />
      <sub>Zrýchlenie a efektivita pre 1920x1080.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <img src="images/sp-2k.png" alt="Graf zrýchlenia a efektivity pre 2560x1440" width="100%" />
      <br />
      <sub>Zrýchlenie a efektivita pre 2560x1440.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/sp-4k.png" alt="Graf zrýchlenia a efektivity pre 3840x2160" width="100%" />
      <br />
      <sub>Zrýchlenie a efektivita pre 3840x2160.</sub>
    </td>
  </tr>
</table>

<a id="profilovanie-v-amd-uprof"></a>
## Profilovanie v AMD uProf

Snímky z AMD uProf boli použité na potvrdenie rozdielu v správaní medzi bežnou implementáciou a verziou s `Parallel.For`.

Na čo sa zamerať:

- V bežnej verzii je počas väčšiny meraného intervalu vyťažený najmä jeden worker.
- V paralelnej verzii je práca rozložená medzi viac vlákien.
- Pri veľkých rozlíšeniach a vysokom počte iterácií paralelná verzia drží vyťažených viac CPU zdrojov a skracuje celkový čas behu.

<table>
  <tr>
    <td align="center" width="50%">
      <img src="images/800x600-iterations-500-sequentail-amd-uprof.png" alt="AMD uProf snímka pre sekvenčné 800x600 a 500 iterácií" width="100%" />
      <br />
      <sub>Sekvenčne, 800x600, 500 iterácií. Workload je krátky a sústredený do jednej vykonávacej vetvy.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/800x600-iterations-500-parallel-16-threads-amd-uprof.png" alt="AMD uProf snímka pre paralelné 800x600 a 500 iterácií s 16 vláknami" width="100%" />
      <br />
      <sub>Paralelne, 800x600, 500 iterácií, 16 vlákien. Práca je rozdelená medzi viac workerov a dokončí sa výrazne rýchlejšie.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <img src="images/4K-iterations-5000-sequentail-amd-uprof.png" alt="AMD uProf snímka pre sekvenčné 4K a 5000 iterácií" width="100%" />
      <br />
      <sub>Sekvenčne, 4K, 5000 iterácií. Ťažký workload, dlhá kritická cesta, úzke miesto v jednom vlákne.</sub>
    </td>
    <td align="center" width="50%">
      <img src="images/4K-iterations-5000-parallel-16-threads-amd-uprof.png" alt="AMD uProf snímka pre paralelné 4K a 5000 iterácií s 16 vláknami" width="100%" />
      <br />
      <sub>Paralelne, 4K, 5000 iterácií, 16 vlákien. Rovnaký algoritmus beží s výrazne vyšším využitím hardvéru.</sub>
    </td>
  </tr>
</table>

<a id="ako-to-funguje"></a>
## Ako to funguje

Pre každý pixel `(px, py)` program namapuje súradnice obrazovky na bod komplexnej roviny `c = x0 + y0*i` a iteruje:

`z(n+1) = z(n)^2 + c`, pričom `z(0) = 0`

Iterácia sa zastaví, keď:

- `|z| > 2`, teda bod unikol, alebo
- sa dosiahne `maxIterations`, takže bod je považovaný za súčasť množiny

Výsledný počet iterácií sa uloží do plochého poľa celých čísel a neskôr sa prevedie na farby.

Pipeline na vysokej úrovni:

1. Vypočítať escape count pre všetky pixely.
2. Previesť počty iterácií na farby.
3. Vyrenderovať bitmapu.
4. Zmerať čas cez viac behov.
5. Nahlásiť medián, aby sa znížil vplyv odľahlých hodnôt.

<a id="ukazky-kodu"></a>
## Ukazky kodu

### Bežný jednovláknový výpočet

```csharp
// Jedno vlákno prechádza všetky riadky a všetky pixely.
for (int py = 0; py < height; py++)
{
    double y0 = startY + py * step; // Imaginárna časť pre aktuálny riadok
    int rowOffset = py * width;     // Počiatočný index riadku v plochom výstupnom poli

    for (int px = 0; px < width; px++)
    {
        double x0 = startX + px * step; // Reálna časť pre aktuálny pixel

        double x = 0.0;
        double y = 0.0;
        double xx = 0.0; // Cache pre x^2
        double yy = 0.0; // Cache pre y^2
        int iteration = 0;

        // Štandardná Mandelbrotova rekurencia:
        // z = z^2 + c, opakovaná, kým bod neunikne alebo nedosiahneme maxIterations.
        while (xx + yy <= 4.0 && iteration < maxIterations)
        {
            y = 2.0 * x * y + y0; // Imaginárna časť z^2 + c
            x = xx - yy + x0;     // Reálna časť z^2 + c

            xx = x * x;
            yy = y * y;
            iteration++;
        }

        data[rowOffset + px] = iteration; // Uloženie escape count pre tento pixel
    }
}
```

### Paralelný výpočet pomocou `Parallel.For`

```csharp
var options = new ParallelOptions
{
    MaxDegreeOfParallelism = MandelbrotConfig.Threads
};

// Každá iterácia spracuje jeden riadok.
// Riadky sa vo výstupnom poli neprekrývajú, preto netreba žiadny lock.
Parallel.For(0, height, options, py =>
{
    double y0 = startY + py * step; // Imaginárna časť pre tento riadok
    int rowOffset = py * width;     // Jedinečný výstupný rozsah vlastnený týmto workerom

    for (int px = 0; px < width; px++)
    {
        double x0 = startX + px * step; // Reálna časť pre aktuálny pixel

        double x = 0.0;
        double y = 0.0;
        double xx = 0.0;
        double yy = 0.0;
        int iteration = 0;

        // Rovnaká matematika ako v sekvenčnej verzii.
        // Mení sa iba rozdelenie riadkov medzi vlákna.
        while (xx + yy <= 4.0 && iteration < maxIterations)
        {
            y = 2.0 * x * y + y0;
            x = xx - yy + x0;

            xx = x * x;
            yy = y * y;
            iteration++;
        }

        data[rowOffset + px] = iteration; // Bezpečný zápis: tento riadok patrí len tomuto workerovi
    }
});
```

### Rýchle renderovanie bitmapy

Projekt rozlišuje aj medzi jednoduchou a rýchlejšou renderovacou cestou:

- `SetPixel`: ľahko pochopiteľné, ale pomalé pri veľkých obrázkoch, pretože sa bitmapa mení pixel po pixeli
- `LockBits`: zapisuje surové pixelové dáta priamo do pamäte, čo je pri veľkých výstupoch výrazne rýchlejšie

To je dôležité, pretože samotné vytváranie obrázka by inak mohlo skresliť interpretáciu benchmarkov.

<a id="spustenie"></a>
## Spustenie

### PowerShell (`start.ps1`)

```powershell
.\start.ps1                          # sekvenčný benchmark
.\start.ps1 -par 4                   # paralelný benchmark, 4 vlákna
.\start.ps1 -Mode image              # sekvenčný image viewer
.\start.ps1 -Mode image -par 4       # paralelný image viewer
.\start.ps1 -Mode all                # benchmark + image, sekvenčne
.\start.ps1 -Mode all -par 4         # benchmark + image, paralelne
.\start.ps1 -cmd                     # výpis benchmarku do konzoly
.\start.ps1 -par 4 -cmd              # výpis paralelného benchmarku do konzoly
```

### Bash (`start.sh`)

```bash
./start.sh                           # sekvenčný benchmark
./start.sh -par 4                    # paralelný benchmark, 4 vlákna
./start.sh image                     # sekvenčný image viewer
./start.sh image -par 4              # paralelný image viewer
./start.sh all                       # benchmark + image, sekvenčne
./start.sh all -par 4                # benchmark + image, paralelne
```

Podporované počty vlákien použité v projekte sú `2`, `4`, `8`, `12` a `16`.

<a id="vstupy"></a>
## Vstupy

- CLI režimy:
  - `--benchmark`
  - `--benchmark-par <threads>`
  - `--image`
  - `--image-par <threads>`
  - `--cmd`
- Konfigurácia v `MandelbrotConfig.cs`:
  - rozlíšenie
  - maximálny počet iterácií
  - stred viewportu a mierka
  - počet warmup behov
  - počet meraných behov
  - počet vlákien

<a id="vystupy"></a>
## Vystupy

### Benchmark reporty

- Sekvenčne: `results/sequential/<report-file>.txt`
- Paralelne: `results/parallel/<WxH>/<report-file>.txt`
- Vzor názvu súboru:
  - `results-{sequential|parallel-<threads>threads}-{WarmupRuns}warmup-{Runs}runs-<WxH>.txt`

### Vyrenderovane obrazky

- Windows Forms viewer zobrazuje celkový čas výpočtu v titulku okna.
- Stlačením `Ctrl+S` sa uloží PNG.
- Výstupné priečinky:
  - Sekvenčne: `mandelbrot-images/sequential/<WxH>/`
  - Paralelne: `mandelbrot-images/parallel/<WxH>/threads-<n>/`

<a id="struktura-projektu"></a>
## Struktura projektu

- `Program.cs` - vstupný bod a prepínanie režimov
- `MandelbrotUtils.cs` - výpočet Mandelbrotovej množiny, renderovanie, benchmarking, reportovanie
- `MandelbrotConfig.cs` - aktívne rozlíšenie, počet iterácií, viewport, počet vlákien
- `MandelbrotForm.cs` - Windows Forms viewer obrázkov
- `start.ps1` - PowerShell runner
- `start.sh` - Bash runner
- `images/` - kurátorské README obrázky a grafy
- `results/` - vygenerované benchmark reporty
- `mandelbrot-images/` - vygenerované výstupné obrázky
- `amduprof-images/` - vygenerované profiling screenshoty

<a id="konfiguracia"></a>
## Konfiguracia

Najdôležitejšie nastavenia sú v `MandelbrotConfig.cs`:

- predvoľby rozlíšenia: `800x600`, `1920x1080`, `2560x1440`, `3840x2160`
- predvoľby iterácií: `500`, `1000`, `2000`, `5000`
- viewport: `CenterX`, `CenterY`, `Scale`
- benchmarking: `WarmupRuns`, `Runs`
- paralelné nastavenia: `Parallel`, `Threads`

<a id="nastavenia-projektu"></a>
## Nastavenia projektu

- SDK: `Microsoft.NET.Sdk`
- Typ výstupu: `Exe`
- Cieľový framework: `net10.0-windows`
- Windows Forms povolené: `true`
- Root namespace: `_3_volitelna`
- `ImplicitUsings`: `enable`
- `Nullable`: `enable`
- `PlatformTarget`: `x64`

Release konfigurácia:

- `Optimize=true`
- `TieredCompilation=true`
- `TieredPGO=true`
- `TieredCompilationQuickJit=false`
- `TieredCompilationQuickJitForLoops=false`
- `ServerGarbageCollection=true`

<a id="testovacie-prostredie"></a>
## Testovacie prostredie

Benchmarky a profiling boli vytvorené na:

- CPU: AMD Ryzen 7 8840HS, 8 jadier / 16 vlákien, Radeon 780M
- RAM: 16 GB DDR5
- OS: Windows 11 Home
- .NET SDK: 10.0.103
- Profiler: AMD uProf 5.2.431.0
