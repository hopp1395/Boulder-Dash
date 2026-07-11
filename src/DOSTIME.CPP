#include <dos.h>
#include <time.h>
#include <stdio.h>
#include <conio.h>


int displaytime(void)
 {
   struct dostime_t time;

   for (;;)
    {
     _dos_gettime (&time);
     gotoxy(1,1);
     printf("%i:%i:%i:%i          ",time.hour,time.minute,time.second,time.hsecond);
     if (kbhit()!=0)
      {
       getch();
       return(0);
      }
    }
 }

void main (void)
 {
  displaytime();

  outp(0x43,0x34);
  outp(0x40,0x00);
  outp(0x40,0x20);

  displaytime();

  outp(0x43,0x34);
  outp(0x40,0x00);
  outp(0x40,0x00);

  displaytime();
 }
