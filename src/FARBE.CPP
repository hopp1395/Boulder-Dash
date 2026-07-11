#include <stdio.h>
#include <conio.h>
#include <dos.h>

union REGS inregs, outregs;     //Standartregister
struct SREGS segregs;           //Segmentregister


void main (void)
 {
  int i,k,j,z;

  inregs.x.ax = 0x0013;                 //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  for (i=0;i<255;i++)
   {
    for (k=i*11;k<i*11+10;k++)
     for (j=i/2;j<10+i/2;j++)
      pokeb(0xA000,k+j*320,i);
   }




  getch();
  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);
 }