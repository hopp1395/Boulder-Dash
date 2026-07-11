#include <conio.h>
#include <stdio.h>


void main (void)
 {
  unsigned int i=1;

  while (i!=0)
   {
    clrscr();
    printf("Dezimalezahl eingeben (0 Ende): ");
    scanf ("%u",&i);
    printf("\nDezimal: %05u   Hexadezimal: %04X",i,i);
    getch();
   }
 }