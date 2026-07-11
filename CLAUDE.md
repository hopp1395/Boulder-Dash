# CLAUDE.md

Diese Datei gibt Claude Code (claude.ai/code) Orientierung für die Arbeit in diesem Repository.

## Worum es geht

Ein Boulder-Dash-Klon für MS-DOS, geschrieben in den 1990ern in Borland Turbo C++ (LARGE-Speichermodell) von Jan Hoppe. Kommentare, Bezeichner und Doku sind auf **Deutsch**. Das Repository ist ein historisches Code-Archiv — alles liegt in `src/`, inklusive kompilierter `.OBJ`-/`.EXE`-Artefakte, BGI-Grafiktreiber und binärer Spieldaten, direkt neben den Quelltexten.

## Bauen und Ausführen

Es gibt kein modernes Buildsystem. Das Spiel wurde mit der Borland-Turbo-C++-IDE über die Projektdatei `src/BOULDER.PRJ` gebaut. Der Code hängt von DOS-spezifischen Headern ab (`conio.h`, `dos.h`, `alloc.h`), greift direkt auf Hardware zu (VGA-Register, Sound Blaster an gesuchtem Port, Timer-Interrupt 0x1C via `setvect`/`getvect`) und nutzt 16-Bit-Segmentspeicher — mit einem modernen Compiler kompiliert er nicht. Zum Bauen oder Ausführen DOSBox (oder Ähnliches) mit installiertem Turbo C++/Borland C++ verwenden; die fertige `src/BOULDER.EXE` läuft direkt in DOSBox.

## Quelltext-Struktur

Das Spiel wird als **eine einzige Übersetzungseinheit** kompiliert: `BOULDER.CPP` ist die Hauptdatei (v1.02, die aktuellste) und bindet die anderen Spielmodule direkt per `#include` ein:

- `BOULDER.CPP` — globale Variablen, Level-Laden, `main()`
- `INIT.CPP` — Initialisierungsroutinen (Grafikmodus, Sprites, Sound-Setup)
- `INTRO.CPP` — Loader/Introscreen, Paletten-Überblendungen
- `GAME.CPP` — Spiellogik: Bewegung, fallende Steine/Diamanten, Gegner, timer-ISR-getriebene Spielschleife

Der Leveleditor ist ein eigenes Programm mit demselben Include-Muster: `LEVELEDI.CPP` bindet `LEVELEDT.CPP` (eingebettete Sprite-Daten) und `MAUS.CPP` (Mausroutinen) ein.

`BOULDER.C`, `BOULDER1.CPP`–`BOULDER4.CPP`, `LEVELOLD.CPP`, `LEVEL1.CPP`, `LEVEL3.CPP` und die `.BAK`-Dateien sind **ältere Versionen, die als Schnappschüsse aufbewahrt werden** — nicht bearbeiten; Änderungen gehören in `BOULDER.CPP` und die davon eingebundenen Dateien.

Eigenständige Hilfsprogramme (jeweils mit eigener `main()`): `CONVATB.CPP` (ASCII-Level → binär), `CONVBTA.CPP` (binär → ASCII), `LCONVERT.CPP` (Level-Konverter), `SPRITES.CPP` (Sprite-Werkzeug), `BINHEX.CPP`, `SOUND4.C`/`SOUND5.C` (Sound-Blaster-Test/-Wiedergabe) sowie kleine Experimente (`TEST.CPP`, `FARBE.CPP`, `TIMER.CPP`, `KEYB.CPP`, `MEM.CPP`).

## Datenformate

`src/BOULDER.TXT` ist die maßgebliche Formatdokumentation:

- **Leveldateien** (`LEVEL*.BIN`/`.DAT`): 16-Byte-Header — X-Größe, Y-Größe, benötigte Diamantenanzahl, Zeitlimit, 4 Grundfarben, Start-X/Y, Umwandlungszeit, Punkte pro Diamant (2 Werte), 3 reservierte Bytes — gefolgt von der Levelmaske (`X*Y` Bytes, ein Byte pro Kachel).
- **Kachel-/Sprite-IDs** sind die `MASK_*`-Konstanten in `BOULDER.CPP` (0=Leer, 1=Erde, 2=Stein, 3=Diamant, 4=Mauer, 5=Stahl, 6=Rockford/Spieler, 7=Lava, 8=Geist, 9=Schmetterling, 10/11=Eingang/Ausgang, 12=Explosion, 13=Mauer-Umwandlung). In der Levelmaske markiert Kachel 10 den Spielerstart und Kachel 11 den Ausgang.
- **Sprites** (`SPRITES.BIN`/`.DAT`): Grundsprites plus Animationsframes, Aufbau in `BOULDER.TXT` und `SPRITES.TXT` beschrieben; der Editor `LEVELEDT.CPP` hält Sprites als eingebettete ASCII-Pixel-Strings.
- Das ASCII-Levelformat (zum Handbearbeiten, konvertiert mit `CONVATB`/`CONVBTA`) verwendet Ziffern-/Buchstabenzeichen pro Kachel — siehe Beispiele in `LEVEL.TXT` und `DECODE01.TXT`.

## Konventionen

- Typen: `BYTE`/`WORD`/`SBYTE`/`SWORD`-Typedefs am Anfang jeder Hauptdatei.
- Starke Nutzung globaler Zustandsvariablen (Bildschirmpuffer `bsp`, `level_kopie`, Timer-Tick-Zähler `clk_1`/`clk_2`/`clk_4`/`clk_18`, getrieben vom eingehängten Timer-Interrupt).
- Neue Kommentare auf Deutsch schreiben, passend zum Bestand.
