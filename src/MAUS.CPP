//Funktionen : maus_ermiteln
//             maus_bereich
//             maus_pos
//             maus_anzeigen
//             maus_loeschen

BYTE maus_ermiteln(void);
void maus_bereich (WORD x1, WORD x2, WORD y1, WORD y2);


int xpos,ypos,knoepfe;		//Maus

BYTE maus_ermiteln(void)
 {
  inregs.x.ax=0x00;
  int86(0x33,&inregs,&outregs);
  return(outregs.h.al);
 }

void maus_bereich (WORD x1, WORD x2, WORD y1, WORD y2)
 {
  inregs.x.ax=0x07;
  inregs.x.cx=x1;
  inregs.x.dx=x2;
  int86(0x33,&inregs,&outregs);
  inregs.x.ax=0x08;
  inregs.x.cx=y1;
  inregs.x.dx=y2;
  int86(0x33,&inregs,&outregs);
 }

void maus_pos(void)
 {
  inregs.x.ax=0x03;
  int86(0x33,&inregs,&outregs);
  Maus_xpos=outregs.x.cx;
  Maus_ypos=outregs.x.dx;
  Maus_knop=outregs.x.bx;
 }

void maus_anzeigen(WORD x, WORD y)
 {
  inregs.x.ax=0x04;
  inregs.x.cx=x;
  inregs.x.dx=y;
  int86(0x33,&inregs,&outregs);
  inregs.x.ax=0x01;
  int86(0x33,&inregs,&outregs);
 }

void maus_loeschen(void)
 {
  inregs.x.ax=0x02;
  int86(0x33,&inregs,&outregs);
 }




