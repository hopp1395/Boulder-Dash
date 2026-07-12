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

**Treue-Prinzip:** Der Port bildet das beobachtbare Verhalten des Originals exakt nach, ist aber idiomatisch aufgebaut — er ist keine Transliteration mehr. Kommentare zitieren die Originalstellen (z. B. „BOULDER.CPP:397-398"), Abweichungen sind an Ort und Stelle begründet. Bei Verhaltensfragen immer die zitierte Originalzeile in `src/` nachschlagen, nicht aus dem Gedächtnis rekonstruieren.

**Objektmodell (`Core/Objects/`):** Jedes Ding im Cave ist eine Klasse — `CaveObject` als abstrakte Basis, darunter die konkreten Objekte. Zwei Zwischenklassen bündeln, was sich zwei Objekte wirklich teilen: `FallingObject` (Stein/Diamant: Fall-Momentum, Abrollen, Landeklang, Zaubermauer-Umwandlung) und `CreatureObject` (Geist/Schmetterling: Blickrichtung, Drehsinn, Explosionsart). `CaveObjects` ist die einzige Stelle, die eine Element-ID auf ihre Klasse abbildet.

**Die Objekte rechnen sich selbst aus.** Jedes kennt seine `Cave` (Konstruktor) und seinen `Index` darin, kann also über die Cave seine Nachbarn ansehen und manipulieren: sich bewegen, sammeln, schieben, sprengen (`CaveObject.Explode`). Die Cave gibt nur den Takt vor und ruft reihum bei jedem Objekt auf:

- `CaveObject.NextFrame()` — ein Animationsschritt, einmal je Tick.
- `CaveObject.Interact()` — der Zug des Objekts, einmal je Cave-Scan. **Hier stehen seine Spielregeln.** Wer keine hat (Erde, Wände, Türen), erbt das leere `Interact()`.
- `CaveObject.EndScan()` — setzt am Ende des Scans `ScannedThisFrame` zurück.

`ScannedThisFrame` (Bit 0x80 im Original) verhindert, dass ein Objekt im selben Scan mehrfach zieht — ohne das liefe ein fallender Stein seiner eigenen Bewegung hinterher, weil zeilenweise von oben nach unten gescannt wird. Jede `Interact()`-Regel beginnt deshalb mit dieser Prüfung. Nebenbei wirkt das Flag als Sperre: Eine gerade geräumte Zelle ist für Rockford unpassierbar und für die Amoeba kein Wuchsgrund.

**Eine eigene Physik-Klasse gibt es nicht mehr** (`CavePhysics` ist aufgelöst): Der Stein weiß, wie er fällt, die Kreatur, wie sie an der Wand entlangläuft, Rockford, wie er gräbt und schiebt. In `Cave.NextState()` bleibt nur, was kein einzelnes Objekt entscheiden kann, weil es den **ganzen** Scan überblicken muss: Rockfords Todeserkennung (er ist ja weg, wenn er stirbt) und das Amoeba-Schicksal, das an der Zählung der gesamten Amoeba aus dem vorigen Scan hängt (`Cave.ReportAmoeba`/`Cave.AmoebaFate`).

Damit ein Objekt das alles kann, ist **die `Cave` die Welt**: Sie hält neben dem Gitter auch `GameState` (Punkte, Quote, Restzeiten, Sound-Ereignisse), `InputState`, `Camera` und den `Random` — sonst könnte ein parameterloses `Interact()` nichts davon erreichen.

Das Cave-Gitter hält diese Objekte, nicht mehr rohe Bytes. Im Original steckten Element-ID und Zustand in den Bits *eines* Bytes (0x0F ID, 0x80 verarbeitet, 0x40 Fall-Momentum, 0x60 Blickrichtung) — weil Stein und Kreatur sich dasselbe Byte teilten, überlappten Momentum- und Richtungsbits. Mit eigenen Feldern je Typ ist das gegenstandslos. `CaveObject.ToRaw()` rechnet den Zustand noch ins Originalbyte zurück, aber nur für Serialisierung und Regressionsvergleiche; die Physik liest keine Bytes mehr. **Die Bytekodierung gilt nur noch im Dateiformat** (`CaveData.Tiles` bleibt `byte[]` — es ist der unveränderliche Dateiinhalt und wird zwischen Durchläufen wiederverwendet).

Auch die **Animation** gehört den Objekten: Die früheren globalen Zähler (`wechsel_vier`, `wechsel_explo`, `wechsel_boulder`) sind weg, jedes Objekt führt seinen eigenen (`AnimationPhase`, `ExplosionObject.ExplosionPhase`, `RockfordObject`). Sie laufen dennoch **synchron** — alle Diamanten einer Cave zeigen dasselbe Bild —, weil `Cave.NextFrame()` sie im selben Takt schaltet und frisch entstehende Objekte mit `Cave.AnimationPhase` geboren werden (`Cave.Spawn`). Auseinanderlaufen würden sie nur, wenn man ihnen verschiedene **Startphasen** gäbe; das wäre eine bewusste Änderung. Dasselbe gilt für die Explosionen: Ihr Zähler war eine globale Variable, die *jede neue Explosion auf 1 zurücksetzte* — auch für anderswo schon halb abgelaufene, weshalb alle gemeinsam verschwanden. `Cave.Explode` tut das weiterhin ausdrücklich für alle (`Cave.RestartExplosions`).

**Ausnahmen zugunsten des C64-Originals (BD1):** Wo das DOS-Original vom C64-Vorbild abweicht, gewinnt inzwischen BD1 — die Spieldaten (Caves, Demo) und der Sound stammen ohnehin von dort. Betroffen ist außerdem das Auf-/Zudecken des Bildschirms (`ScreenCover`): 69 Runden mit je einer Zufallsposition pro Cave-Zeile, danach vollständiges Aufdecken; der Eingangsaufbau (Blinken, `EntranceProgress`-Zähler) läuft dabei schon mit, aber die Physik ruht — Steine/Diamanten fallen und Gegner bewegen sich erst, wenn die Höhle komplett aufgedeckt ist und das Startsignal (die Eingangs-Explosion) ertönt ist (`GameTick`). Am Cave-Ende deckt sich der Bildschirm wieder zu, die Physik läuft dabei weiter. Das DOS-Original machte beides anders (`level_in()` räumte 4 Zufallszellen der Sichtfenster-Maske pro Tick und startete `regel()` erst danach; `level_out()` scrollte den Bildschirm hoch statt zuzudecken).

Nach BD1 geht auch die **Amoeba** (`AmoebaObject`, maßgeblich ist die BDCFF-Objektspezifikation 000A auf elmerproductions.com/sp/peterb/BDCFF/objects/000A.html): Jede Amoeba-Zelle würfelt pro Cave-Scan einzeln, ob sie wächst — mit 4/128 (~3 %), nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) —, und wächst dann in genau eine zufällig gezogene der vier Richtungen, sofern dort Leerraum oder Erde liegt. Ab 200 Zellen wird sie ganz zu Boulders, eingeschlossen zu Jewels; beides greift erst im Folge-Scan, „zu groß" hat Vorrang. Die Amoeba-Zeit steht als `[Rules] AmoebaTime` in der Cave-Datei und läuft wie die Zaubermauer-Zeit in Spielsekunden ab; in BD1 stammt sie aus **demselben Cave-Kopf-Byte $01 wie die Zaubermauer-Zeit** (beide sollten laut Datenformat nie in derselben Cave vorkommen), deshalb ist `AmoebaTime` in allen 100 Dateien gleich `MagicWallTime` — `AmoebaTests` prüft das. Das DOS-Original machte es viel simpler: pro Scan wuchs nur eine einzige, per `rand()%96` gewählte Zelle, immer in fester Richtungspriorität, und ab 96 Zellen wandelte sie um (`BOULDER.CPP:745-755, 937-956`, dort „lava" genannt).

Ebenfalls nach BD1 geht das **Spieltempo** (`CaveSpeed`): Es hängt am Schwierigkeitsgrad, nicht an der Cave (BD1 verzögert pro Cave-Scan 90 Zyklen je Einheit `CaveDelay` = 12/6/3/1/0 für Grad 1–5; Intermissions scannen auf kleinerer Basis und damit schneller). Wie alle Spieldaten steht es trotzdem in der Cave-Datei selbst (`[Rules] GameSpeed` = Millisekunden pro Cave-Scan, WYSIWYG) und hängt als `CaveData.GameSpeed` an der geladenen Cave — da eine Cave-Datei genau eine Cave auf genau einem Level ist, passt das zusammen. `CaveSpeed.For(level, isIntermission)` ist die Herleitung dieser Werte; sie wird zum Laden nicht benutzt, sondern prüft im Test die 100 ausgelieferten Dateien gegen BD1. Umgesetzt über die Tickrate — die Physik läuft weiter alle 3 Ticks, also skalieren Animation, Kamera-Scroll, Eingangsaufbau und `ScreenCover` korrekt mit. Nur die **Spielsekunde** darf das nicht: sie zählt in BD1 IRQ-getrieben und ist tempo-unabhängig (und mit ~64 statt 60 Ticks länger als eine echte Sekunde). Sie bleibt bei allen Graden bei 1,1 realen Sekunden — dafür wird die `clk_18`-Periode pro Tempo nachgeführt (Original: fix 22).

Ebenfalls nach BD1 gehen die **Menüs** (`SessionPhase.TitleScreen`/`Menu`, `TitleRenderer`): erst der reine Titelbildschirm (Logo + First-Star-Schriftzug), eine beliebige Taste öffnet den Option-Screen mit der CAVE/LEVEL-Auswahl — wählbar sind wie in BD1 nur die Caves **A, E, I, M**, und auf Grad 4/5 ist Cave A erzwungen (Handbuch). Nach Leerlauf (`GameSession.AttractIdleSeconds`, 12 s als Annahme) startet automatisch die Demo (Attract-Mode); Tastendruck oder Demo-Ende führen zum Titel zurück. Die frühere `DemoWait`-Phase (DOS-`delay(7000)`) entfällt — die BD1-Aufzeichnung enthält ihre Anlauf-Wartezeit selbst (`demo.txt` beginnt mit „Wait 15“). Die beiden Logo-Grafiken liegen als `Screens/title.txt` und `Screens/option-logo.txt` im Sprite-Textformat (aus Referenz-Screenshots quantisiert) und werden mit festen C64-Farben gerendert, nicht über die Cave-Palette; die Textzeilen zeichnet der `BiosFont`. Das Mauermuster im Hintergrund läuft wie im Original vertikal durch — derselbe Gleitfenster-Mechanismus und 8-Phasen-Takt wie bei der Zudeck-Stahlwand (`TitleRenderer` erkennt die Musterzellen beim Laden und macht sie transparent; die beiden Screenshots belegen die Animation, ihr Muster unterscheidet sich exakt um eine vertikale Phase). Das DOS-Menü (Marquee, F-Tasten-Legende, Kachelrahmen, freie Cave-Wahl A–P, F2-Demo) ist ersetzt; nur der Testmodus (F5, auf dem Option-Screen unsichtbar) behält den alten DOS-Look (`TestMenuRenderer`).

Nach BD1 geht schließlich der **Bonusüberlauf am Cave-Ende**: Die **Nullsekunde wird noch ausgespielt** — steht die Anzeige auf 000, läuft die Cave eine volle Spielsekunde weiter, und erst am folgenden Sekundentakt beendet der Zeitablauf sie (`GameTick`). Zieht Rockford in dieser Gnadensekunde in den Ausgang, startet die Bonuszählung bei 0 und der Byte-Zähler läuft über: die Zeitanzeige rasselt von 255 herunter und der Spieler kassiert 255 Gratispunkte (`GameSession.BeginLevelEndBonus`). Bloßer Zeitablauf ohne Ausgang löst das nicht aus. Das DOS-Original kannte den Quirk nicht: dort beendet `if (level_clk==0) level_ende=0xFF;` die Cave sofort bei 0 und steht dazu noch vor dem `regel()`-Aufruf (`BOULDER.CPP:251-255`), und `Level_End()` prüft vor dem ersten Zählschritt (`GAME.CPP:54`).

Nach BD1 geht zuletzt **Rockfords Ruheanimation** („tapping foot and blinking", maßgeblich ist die BDCFF-Objektspezifikation 0006 auf elmerproductions.com/sp/peterb/BDCFF/objects/0006.html): Steht Rockford still, würfelt `GameTick` zu Beginn jeder 8-Frame-Animationssequenz (= bei jedem Umlauf von `Cave.AnimationPhase`) neu aus, ob er in dieser Sequenz blinzelt (1/4) und ob das Fußtappen umschaltet (1/16) — Letzteres ist ein Dauerzustand, kein Einzelereignis. Gewürfelt wird bewusst im `GameTick` und nicht im `RockfordObject`, und zwar auch dann, wenn Rockford noch gar nicht auf dem Feld steht: Es ist derselbe Zufallsstrom, aus dem die Amoeba ihr Wachstum zieht — hinge die Zahl der Ziehungen daran, ob Rockford schon erschienen ist, verschöbe sich die ganze Folge und mit ihr das Verhalten der Cave. Beides ist unabhängig voneinander, weil der C64 obere und untere Körperhälfte getrennt steuert; in Bewegung tut er weder das eine noch das andere. `RockfordObject.Appearance` setzt das Bild deshalb aus zwei Hälften zusammen (`TileAppearance.BottomFrame`): Die Sprite-Frames sind genau dafür gemacht — 14/15 ändern gegenüber dem Ruheframe 13 ausschließlich die Augenpartie, 16/17 ausschließlich Arme und Füße. Das DOS-Original hatte die Animation nie fertiggestellt (`boulder_wait()`, `BOULDER.CPP:648-663`, ist auskommentiert), seine Frames lagen bis dahin brach; gespiegelt wird — wie schon dort (`boulder_lauf()` vertauscht die Pixel nur der Frames 18–23) — allein der Laufzyklus.

Schichten in Core:

- **Objects/** — die Spielobjekte selbst (siehe Objektmodell oben): `CaveObject` plus die 16 konkreten Klassen, `CaveObjects` (Registry), `TileAppearance`/`RenderContext` (der MonoGame-freie Zeichenauftrag eines Objekts an die Rendering-Schicht). Ein Objekt bringt alles mit, was es ausmacht: Kartenglyphe, Sprite-Frames, Spielregeln — früher lag das über vier Tabellen und zwei `switch`-Blöcke verstreut.

  Wo ein Objekt auf ein anderes wirkt, **fragt** es nicht nach dessen Eigenschaften, sondern **sagt ihm Bescheid** und lässt es selbst entscheiden (doppelte Dispatch). Zwei Fälle: `Detonate()` — eine Explosion erfasst die Kachel, der Normalfall ist draufgehen, Stahl und die Türen überschreiben es und halten stand. `ReceiveFalling()` — ein Stein fällt auf die Kachel, der Normalfall ist landen, Leerraum lässt ihn durch, die Mauer lässt ihn abrollen, die Zaubermauer wandelt ihn um, Rockford stirbt. `FallingObject.Interact()` besteht deshalb nur noch aus einer Zeile: `Neighbour(Cave.Width).ReceiveFalling(this)`.
- **Simulation/** — der Kern: `Cave` ist Gitter und Welt zugleich (siehe Objektmodell oben) — sie hält die Objekte und alles, was der ganzen Höhle gehört. `GameTick` ist das Äquivalent der Original-Timer-ISR: Er treibt pro Tick `Clocks` (clk_1/4/18-Zähler), `Camera`, die Countdowns (Restzeit, Zaubermauer, Amoeba-Zeit), den Eingangsaufbau — und gibt der Cave den Takt für `NextFrame()` (Animation, jeden Tick) und `NextState()` (Physik, jeden 3. Tick). Er kennt keine Spielregeln; die stehen in den Objekten. Der Zufall (Amoeba-Wachstum, Steinschub, Rockfords Ruheanimation, `ScreenCover`) kommt aus einem `System.Random` mit festem Seed, das `GameSession` durchreicht — damit bleiben Demo und Golden-State deterministisch; **die Reihenfolge und Anzahl der Ziehungen ist deshalb verhaltensrelevant**. `Rgb`/`Palette` halten nur noch den Farbtyp und die Blitzfarben des Ausgangs — die Cave-Farben selbst stehen als RGB in der Cave-Datei.
- **Flow/** — `GameSession`: die Zustandsmaschine über allem (Menü, Spielen, Demo, Tod/Bonus/Übergangspausen — `SessionPhase`), Cave-Progression über die BD1-`PlayOrder` (A B C D **Q** E F G H **R** … — Intermission nach je 4 Caves), Schwierigkeitsgrad 1–5 wählt die Level-Datei und das Tempo (`CaveSpeed`). `DemoPlayer` spielt die Demo-Züge taktgenau ab.
- **Data/** — Parser/Repositories. **Alle Spieldaten sind menschenlesbare Textdateien** unter `BoulderDash.Game/Assets/` (WYSIWYG — was in der Datei steht, wird geladen; es gibt keinen Binär-/Generierungspfad mehr):
  - `Caves/cave-{A..T}-{1..5}.txt` (100 Stück, Original-BD1-Caves): `[Cave]`/`[Rules]`-Kopf plus `[Map]` als ASCII-Raster (Legende in `CaveAsciiMap`). Laden per Name über `ICaveRepository.Get("cave-A-1")`. `[Cave] Color1`–`Color4` sind die 4 Farben der Cave-Palette als **RGB-Werte** (`#RRGGBB`; Color1 = Palettenindex 0 … Color4 = Index 3 — also genau die Farben, mit denen die Sprite-Glyphen `.`/`:`/`x`/`#` gezeichnet werden). Es gibt keine Farbtabelle und keine Indizes mehr: Die 16-Farben-Tabelle des DOS-Originals (6-Bit-VGA-DAC-Werte aus `setnewpalette`) ist einmalig auf 8 Bit pro Kanal umgerechnet und in die Dateien eingeflossen, das Bild bleibt dadurch unverändert.
  - `Sprites/*.txt` (15 Stück, eine Datei pro Objekt, benannt nach **BDCFF**: diamond, magic-wall, steel-wall, …): `[Frame N]`-Abschnitte, ein Glyph pro Pixel (`.`/`:`/`x`/`#` = Palettenindex 0–3). Achtung: `#` ist zugleich Kommentarzeichen und Pixelglyph — Kommentare gelten nur außerhalb der `[Frame]`-Abschnitte. `SpriteTextRepository.Manifest` legt die Lade-Reihenfolge fest (= Rohsprite-Indizes 0–48, referenziert von `SpriteTables.FrameToRawSprite`, der z_zeiger-Tabelle aus INIT.CPP).
  - `demo.txt`: die Original-BD1-Demo als Zug-Liste („Right 7", „Wait 15"), ein Zug = eine clk_1-Periode.
  - `Screens/title.txt` und `Screens/option-logo.txt`: die beiden BD1-Titelgrafiken (320×200 bzw. 320×152) im Sprite-Textformat, mit festen C64-Farben gerendert (siehe BD1-Ausnahmen und `TitleRenderer`).
- **Audio/** — vollständig synthetisierter Sound (`SidSynth`, `SoundRecipes`, `ThemeTune` nach Peter Broadribbs C64-Analyse, elmerproductions.com/sp/peterb/) — es gibt keine Audiodateien.

In Game: `SpriteAtlas` hält die 49 Rohsprites als Texturen und färbt sie bei jedem Palettenwechsel per SetData mit der 4-Farben-Cave-Palette neu ein (wie das Original per VGA-DAC). `CaveRenderer` zeichnet nur noch das Sichtfenster; **welches Bild eine Kachel zeigt, entscheidet das Objekt selbst** (`CaveObject.Appearance` liefert eine `TileAppearance`, die der `SpriteAtlas` ausführt). Die Frameauswahl liegt damit in Core und ist headless prüfbar.

**Skalierung (keine Original-Entsprechung):** Zwei unabhängige Zooms, gemerkt in `%APPDATA%\BoulderDash\settings.json` (`GameSettings`/`SettingsFile`, tolerant gegen fehlende/defekte Datei). Ohne Einstellungsdatei (erster Start) läuft das Spiel im **Vollbild mit der vollen Cave** (`ViewportSize.Full`, 40×22) — also mit dem größtmöglichen sichtbaren Bereich; die Fenstergröße aus `GameSettings.DefaultWindow*` greift erst, wenn der Spieler das Vollbild per F11 verlässt. *Bildschirm-Zoom*: das Fenster ist frei skalierbar, F11 schaltet randloses Vollbild; gezeichnet wird auf ein RenderTarget in logischer Auflösung, das ganzzahlig und zentriert hochskaliert wird (PointClamp, schwarzer Rand). *Spielflächen-Zoom*: +/− ändern die Sichtfenstergröße in Stufen von 20×12 (Original) bis zur vollen Cave 40×22 (`ViewportSize`, hängt als `Camera.Viewport` an der Kamera); das Fenster wächst dabei mit, damit die Kacheln gleich groß bleiben. Die Scroll-Auslöser und -weiten in `RockfordObject.ScrollCamera` leiten sich aus der Sichtfenstergröße ab und ergeben bei 20×12 exakt die Originalwerte (16/8/7/5); zeigt das Sichtfenster die ganze Cave, wird gar nicht mehr gescrollt. Ist das Sichtfenster größer als die Cave (Intermissions), klemmt die Kamera auf 0 und `CaveRenderer` zentriert die Cave. Die Menübildschirme (Titel/Option/Testmodus) bleiben fest 320×200. Die Kamera-Startposition (Eingang mittig) rechnet seitdem `Camera.CenterOn` beim Cave-Start statt der Cave-Parser.

### Konventionen

- Bezeichner englisch mit dem Vokabular des Original-Handbuchs (`doc/Boulder_Dash.pdf`): Boulder, Jewel, Firefly, Butterfly, Amoeba, EnchantedWall, TitaniumWall, Entrance, EscapeDoor, Cave, Chances, JewelQuota. Ausnahme: die Sprite-*Dateinamen* folgen den BDCFF-Objektnamen (diamond statt jewel usw.).
- Kommentare und Testnamen deutsch; Verhaltens-Kommentare zitieren die Originalquelle mit Datei:Zeile.
- **Golden-Tests als Regressionsnetz.** `GoldenStateTests` spielt die Demo deterministisch durch, `GoldenCaveScanTests` taktet sechs Caves ohne Eingabe headless durch und hasht jeden Gitterzustand. Zusammen sichern sie Physik, Timing und RNG gemeinsam ab — sie sind das Werkzeug, mit dem sich ein Umbau als *verhaltensgleich* nachweisen lässt. Schlägt einer nach einer absichtlichen Verhaltensänderung fehl, die Werte neu einfrieren (die Testausgabe nennt den berechneten Hash); schlägt er nach einem reinen Refactoring fehl, den Fehler suchen und **nicht** den Hash anpassen. Die Demo allein genügt dafür nicht: Sie läuft nur auf Cave A, und die enthält weder Kreaturen noch Amoeba noch Zaubermauer — dafür ist `GoldenCaveScanTests` da.

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
