# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Worum es geht

Ein Boulder-Dash-Klon von Jan Hoppe in zwei Ausprägungen:

- **`csharp/`** — die aktive Neuimplementierung in C#/MonoGame (hier findet die Entwicklung statt). Sie bildet das beobachtbare Verhalten des DOS-Originals exakt nach, ist aber idiomatisch neu aufgebaut.
- **`src/`** — das historische Code-Archiv: das MS-DOS-Original aus den 1990ern (Borland Turbo C++, LARGE-Speichermodell) inklusive kompilierter `.EXE`/`.OBJ`-Artefakte und binärer Spieldaten. **Nur lesen, nie verändern** — es dient als Referenz für das Portierungsverhalten.

Daneben: `doc/Boulder_Dash.pdf` (Original-Handbuch von First Star Software — Quelle der englischen Fachbegriffe), `c64/` (C64-Tape-Image), `sound/` (Referenz-Audiodateien, im Port nicht mehr verwendet).

Kommentare, Doku und Testnamen sind auf **Deutsch**; Bezeichner im C#-Code auf Englisch (siehe Konventionen).

## C#-Port (csharp/)

### Befehle

Benötigt .NET 10 SDK. Solution im `.slnx`-Format.

```powershell
dotnet build csharp/BoulderDash.slnx          # bauen
dotnet test csharp/BoulderDash.slnx           # alle Tests (xUnit)
dotnet test csharp/BoulderDash.slnx --filter "FullyQualifiedName~GameSessionTests"   # eine Testklasse
dotnet test csharp/BoulderDash.slnx --filter "DisplayName~Tod_kostet_ein_Leben"      # ein einzelner Test
dotnet run --project csharp/BoulderDash.Game  # Spiel starten
```

### Architektur

Drei Projekte:

- **BoulderDash.Core** — komplette Spiellogik, Datenparser und Sound-Synthese, ohne externe Abhängigkeiten (kein MonoGame). Headless voll simulierbar — so testen die Tests und Probeläufe ganze Spielsitzungen ohne Fenster.
- **BoulderDash.Game** — dünne MonoGame-DesktopGL-Schale: Rendering, Audio-Ausgabe, Tastatur-Adapter. Keine Content-Pipeline (kein MGCB); `Assets/**` wird per Glob ins Ausgabeverzeichnis kopiert und zur Laufzeit direkt geladen.
- **BoulderDash.Tests** — xUnit. `TestPaths` findet die Repo-Wurzel über die Existenz von `CLAUDE.md` (diese Datei umbenennen/verschieben bricht die Tests).

**Treue-Prinzip:** Der Port ist eine verhaltensgetreue Transliteration des DOS-Originals. Kommentare zitieren die Originalstellen (z. B. „BOULDER.CPP:397-398"); Original-Quirks sind bewusst übernommen und als solche dokumentiert (z. B. überschreibt jeder Tastendruck in `InputState` Flags/Richtung komplett). Bei Verhaltensfragen immer die zitierte Originalzeile in `src/` nachschlagen, nicht aus dem Gedächtnis rekonstruieren.

Schichten in Core:

- **Simulation/** — der Kern: `GameTick` ist das Äquivalent der Original-Timer-ISR und treibt pro Tick `Clocks` (clk_1/4/18-Zähler), `CavePhysics` (regel(): fallende Steine/Diamanten, Gegner, Amoeba, Explosionen), Animationszähler und `Camera`. `BorlandRandom` repliziert den Borland-`rand()` (wichtig für Determinismus/Golden-State), `InputState` die Mov_Rockford-Scancode-Semantik, `Palette` die 6-Bit-VGA-DAC-Farben.
- **Flow/** — `GameSession`: die Zustandsmaschine über allem (Menü, Spielen, Demo, Tod/Bonus/Übergangspausen — `SessionPhase`), Cave-Progression über die BD1-`PlayOrder` (A B C D **Q** E F G H **R** … — Intermission nach je 4 Caves), Schwierigkeitsgrad 1–5 wählt die Level-Datei. `DemoPlayer` spielt die Demo-Züge taktgenau ab.
- **Data/** — Parser/Repositories. **Alle Spieldaten sind menschenlesbare Textdateien** unter `BoulderDash.Game/Assets/` (WYSIWYG — was in der Datei steht, wird geladen; es gibt keinen Binär-/Generierungspfad mehr):
  - `Caves/cave-{A..T}-{1..5}.txt` (100 Stück, Original-BD1-Caves): `[Cave]`/`[Rules]`-Kopf plus `[Map]` als ASCII-Raster (Legende in `CaveAsciiMap`). Laden per Name über `ICaveRepository.Get("cave-A-1")`.
  - `Sprites/*.txt` (15 Stück, eine Datei pro Objekt, benannt nach **BDCFF**: diamond, magic-wall, steel-wall, …): `[Frame N]`-Abschnitte, ein Glyph pro Pixel (`.`/`:`/`x`/`#` = Palettenindex 0–3). Achtung: `#` ist zugleich Kommentarzeichen und Pixelglyph — Kommentare gelten nur außerhalb der `[Frame]`-Abschnitte. `SpriteTextRepository.Manifest` legt die Lade-Reihenfolge fest (= Rohsprite-Indizes 0–48, referenziert von `SpriteTables.FrameToRawSprite`, der z_zeiger-Tabelle aus INIT.CPP).
  - `demo.txt`: die Original-BD1-Demo als Zug-Liste („Right 7", „Wait 15"), ein Zug = eine clk_1-Periode.
- **Audio/** — vollständig synthetisierter Sound (`SidSynth`, `SoundRecipes`, `ThemeTune` nach Peter Broadribbs C64-Analyse, elmerproductions.com/sp/peterb/) — es gibt keine Audiodateien.

In Game: `SpriteAtlas` hält die 49 Rohsprites als Texturen und färbt sie bei jedem Palettenwechsel per SetData mit der 4-Farben-Cave-Palette neu ein (wie das Original per VGA-DAC); `CaveRenderer` zeichnet das 20×12-Sichtfenster und wählt die Animationsframes.

### Konventionen

- Bezeichner englisch mit dem Vokabular des Original-Handbuchs (`doc/Boulder_Dash.pdf`): Boulder, Jewel, Firefly, Butterfly, Amoeba, EnchantedWall, TitaniumWall, Entrance, EscapeDoor, Cave, Chances, JewelQuota. Ausnahme: die Sprite-*Dateinamen* folgen den BDCFF-Objektnamen (diamond statt jewel usw.).
- Kommentare und Testnamen deutsch; Verhaltens-Kommentare zitieren die Originalquelle mit Datei:Zeile.
- `GoldenStateTests` (deterministischer Demo-Durchlauf mit eingefrorenem Hash) ist derzeit geskippt — die eingefrorenen Werte stammen noch vom alten `DEMO.BIN`-Lauf und müssen nach Abnahme der BD1-Demo neu ermittelt werden (Vorgehen steht im Klassenkommentar).

## Historisches Archiv (src/)

### Bauen und Ausführen

Kein modernes Buildsystem. Gebaut wurde mit der Borland-Turbo-C++-IDE über `src/BOULDER.PRJ`. Der Code hängt von DOS-spezifischen Headern ab (`conio.h`, `dos.h`, `alloc.h`), greift direkt auf Hardware zu (VGA-Register, Sound Blaster, Timer-Interrupt 0x1C via `setvect`/`getvect`) und nutzt 16-Bit-Segmentspeicher — mit einem modernen Compiler kompiliert er nicht. Zum Bauen/Ausführen DOSBox mit installiertem Turbo C++ verwenden; die fertige `src/BOULDER.EXE` läuft direkt in DOSBox.

### Quelltext-Struktur

Das Spiel wird als **eine einzige Übersetzungseinheit** kompiliert: `BOULDER.CPP` (v1.02, die aktuellste) bindet die anderen Module direkt per `#include` ein:

- `BOULDER.CPP` — globale Variablen, Level-Laden, `main()`
- `INIT.CPP` — Initialisierung (Grafikmodus, Sprite-Zeiger/z_zeiger-Tabelle, Sound-Setup)
- `INTRO.CPP` — Loader/Introscreen, Paletten-Überblendungen, Sprite-Laden
- `GAME.CPP` — Spiellogik: Bewegung (Mov_Rockford), regel(), timer-ISR-getriebene Spielschleife

Der Leveleditor ist ein eigenes Programm mit demselben Include-Muster: `LEVELEDI.CPP` bindet `LEVELEDT.CPP` und `MAUS.CPP` ein. `BOULDER.C`, `BOULDER1.CPP`–`BOULDER4.CPP`, `LEVELOLD.CPP`, `LEVEL1.CPP`, `LEVEL3.CPP` und die `.BAK`-Dateien sind ältere Schnappschüsse. Eigenständige Hilfsprogramme (je eigene `main()`): `CONVATB`/`CONVBTA` (Level ASCII↔binär), `LCONVERT`, `SPRITES.CPP`, `BINHEX.CPP`, `SOUND4.C`/`SOUND5.C` sowie kleine Experimente.

### Binäre Datenformate (nur Archiv — der C#-Port nutzt sie nicht mehr)

`src/BOULDER.TXT` ist die maßgebliche Formatdoku:

- **Leveldateien** (`LEVEL*.BIN`/`.DAT`): 16-Byte-Header (X/Y-Größe, Diamantenquote, Zeitlimit, 4 Grundfarben, Start-X/Y, Umwandlungszeit, 2× Punkte pro Diamant, 3 reserviert), danach die Levelmaske (`X*Y` Bytes, ein Byte pro Kachel). Kachel-IDs = `MASK_*`-Konstanten in `BOULDER.CPP` (0=Leer … 13=Mauer-Umwandlung; 10/11 = Eingang/Ausgang).
- **Sprites** (`SPRITES.BIN`): 49 Einträge à 16×16 Pixel (Eintrag 48: 16×24), 1 Byte/Pixel = Palettenindex 0–3, nach jedem Eintrag 2 Füllbytes. Achtung: `src/SPRITES.TXT` ist KEINE Doku, sondern ein byte-identisches Duplikat der BIN-Datei.
- **Demo** (`DEMO.BIN`): rohe Tastatur-Scancodes, Terminator 0x31.
