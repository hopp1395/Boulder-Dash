#include <conio.h>
#include <stdio.h>


void main (void)
 {
  unsigned char code ;

  clrscr();

  while (code!=27)
   {
    code=getch();
    printf("%X  ",code);
   }


 }