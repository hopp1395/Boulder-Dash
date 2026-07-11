# Boulder-Dash

Ein Boulder-Dash-Klon für MS-DOS, geschrieben in den 1990er Jahren in Borland Turbo C++ (LARGE-Speichermodell). Dieses Repository ist ein historisches Code-Archiv: In `src/` liegen die Quelltexte zusammen mit den damals kompilierten Programmen (`.EXE`/`.OBJ`), den BGI-Grafiktreibern und den binären Spieldaten (Level, Sprites, Sound).

## Spielen

Die fertige `src/BOULDER.EXE` läuft direkt in [DOSBox](https://www.dosbox.com/):

```
mount c W:\repos\Boulder-Dash\src
c:
BOULDER.EXE
```

Gesteuert wird mit den Pfeiltasten. Ziel jedes Levels: genügend Diamanten einsammeln und den Ausgang erreichen, bevor die Zeit abläuft. Das Spiel unterstützt Sound-Blaster-Ausgabe, sofern eine Karte gefunden wird.

## Bauen

Es gibt kein modernes Buildsystem — der Code verwendet DOS-spezifische Header (`conio.h`, `dos.h`, `alloc.h`), direkte Hardwarezugriffe (VGA, Sound Blaster, Timer-Interrupt) und 16-Bit-Segmentspeicher. Gebaut wird mit der Borland-Turbo-C++-IDE unter DOS/DOSBox über die Projektdatei `src/BOULDER.PRJ`. Die Hauptdatei `BOULDER.CPP` bindet die übrigen Spielmodule (`INIT.CPP`, `INTRO.CPP`, `GAME.CPP`) direkt per `#include` ein.

## Inhalt des Repositorys

- **Spiel**: `BOULDER.CPP` (aktuelle Version 1.02) mit `INIT.CPP`, `INTRO.CPP`, `GAME.CPP`; ältere Stände als `BOULDER.C` und `BOULDER1.CPP`–`BOULDER4.CPP` erhalten
- **Leveleditor**: `LEVELEDI.CPP` (mit `LEVELEDT.CPP` und `MAUS.CPP`), maus-bedient
- **Werkzeuge**: `CONVATB`/`CONVBTA` (Level zwischen ASCII- und Binärformat konvertieren), `LCONVERT`, `SPRITES` sowie diverse kleine Test- und Experimentierprogramme
- **Daten**: `LEVEL*.BIN`/`.DAT` (Level), `SPRITES.BIN`/`.DAT` (Grafiken), `SOUND.BIN` (Samples), `DEMO.BIN`
- **Doku**: `BOULDER.TXT` beschreibt das Sprite-Layout und das binäre Levelformat (16-Byte-Header plus Levelmaske, ein Byte pro Kachel)

Weitere Details zur Struktur und zu den Datenformaten stehen in der [CLAUDE.md](CLAUDE.md).

## C#-Port

Unter `csharp/` liegt eine moderne Neuimplementierung in C# mit MonoGame, die das beobachtbare Spielverhalten des Originals exakt nachbildet, aber architektonisch idiomatisch neu aufgebaut ist. Die synthetisierten Soundeffekte und die Titelmelodie orientieren sich an Peter Broadribbs Analyse der Original-C64-Soundeffekte auf der [Boulder-Dash-Seite von elmerproductions.com](https://www.elmerproductions.com/sp/peterb/).
