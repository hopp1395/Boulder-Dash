#include <conio.h>
#include <stdio.h>

void main (void)
 {
  clrscr();
  asm {
       mov      ax,0xFFFF;
       mov      cx,0xFFFF;

       mov      si,0x0000;
       mov      di,0x0000;

       cld
       cli
       rep      movsw;
       sti
      }
  printf("Fertig!");
  getch();
 }
