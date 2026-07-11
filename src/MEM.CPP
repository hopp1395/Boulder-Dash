#include <conio.h>
#include <stdio.h>
#include <dos.h>

union REGS inregs, outregs;     //Standartregister
struct SREGS segregs;           //Segmentregister


void main (void)
 {
  clrscr();

  inregs.h.ah = 0x48;
  inregs.x.bx = 4000;
  int86 (0x21,&inregs,&outregs);

  printf("Block Adresse :%05X",outregs.x.ax);
  printf("\nGr”áter Block :%05X",outregs.x.bx);
  getch();

  inregs.h.ah = 0x49;
//  segregs.es =
  int86x (0x21,&inregs,&outregs,&segregs);


 }
