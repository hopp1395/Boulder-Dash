#include <stdio.h>
#include <dos.h>
#include <conio.h>

#define INTR 0X1C    //Timer interrupt

#ifdef __cplusplus
    #define __CPPARGS ...
#else
    #define __CPPARGS
#endif

void interrupt ( *alterinterrupt)(__CPPARGS);

int sekunden=0;

void interrupt neuerinterrupt(__CPPARGS)
{
   sekunden++;
   alterinterrupt();
}

int main(void)
{
   clrscr();
   char c;
   alterinterrupt = getvect(INTR);
   setvect(INTR, neuerinterrupt);

   while (c!=13)
    {
     gotoxy(1,1); printf("%i  ",sekunden);
     if (kbhit()!=0) c=getch();
    }

   setvect(INTR, alterinterrupt);

   return 0;
}

