/**************************************************************************/
/* Programmm    : Boulder Dash                                            */
/* Autor        : Jan Hoppe                                               */
/* Version      : 1.0                                                     */
/* Beschreibung :                                                         */
/**************************************************************************/

/**************************************************************************/
/* INCLUDE Dateien                                                        */
/**************************************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <process.h>
#include <alloc.h>
#include <dos.h>
#include <mem.h>


/**************************************************************************/
/* Konstante Variablen                                                    */
/**************************************************************************/

/**************************************************************************/
/* Selbstdefinierte Datentypen                                            */
/**************************************************************************/

union REGS inregs, outregs;     //Standartregister
struct SREGS segregs;           //Segmentregister

typedef unsigned char   BYTE;
typedef unsigned int    WORD;
typedef signed char     SBYTE;
typedef signed int      SWORD;

void copy64 (BYTE rel);

char *d_name="SPRITES.BIN";
void laden (void);
FILE *dz;
BYTE *sprite = NULL;
WORD setup[7];
unsigned int groesse = NULL;
BYTE buffer[128];
BYTE anzeige[80];

/**************************************************************************/
/* Hauptprogramm                                                          */
/**************************************************************************/

void decodieren (WORD x)
 {
  int i,j,k;

  BYTE *zeiger1=buffer;
  BYTE *zeiger2=sprite+(x*setup[1]);
  BYTE *zeiger3=sprite+(x*setup[1]);

  for (i=0;i<setup[1];i++) anzeige[i] = *(zeiger3++);

  for (i=0;i<32;i++)
   {
    j=*zeiger2;
    *zeiger1 = j >> 6; zeiger1++;
    *zeiger1 = (j & 0x30) >> 4; zeiger1++;
    *zeiger1 = (j & 0x0C) >> 2; zeiger1++;
    *zeiger1 = j & 0x03; zeiger1++;
    zeiger2++;
   }

  for (i=0;i<128;i++)
   {
    if (buffer[i] == 0) buffer[i] = setup[2];
    if (buffer[i] == 1) buffer[i] = setup[3];
    if (buffer[i] == 2) buffer[i] = setup[4];
    if (buffer[i] == 3) buffer[i] = setup[5];
   }
 }

void main (void)
 {
  char c=0;
  char k=0;
  int x,y,i;
  int pos=0;
  BYTE rel=0;

  setup[0] = 0;
  setup[1] = 32;
  setup[2] = BLACK;
  setup[3] = BROWN;
  setup[4] = DARKGRAY;
  setup[5] = WHITE;

  clrscr();
  inregs.x.ax = 0x0003;                //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  laden();

  while (c!=27)
   {
    clrscr();
    decodieren(pos);
    gotoxy(1,1);
    printf("Offset: %04X    Relativ: %02X\n",pos*setup[1]+0x123,rel);
    printf("Position: %03i\nInhalt: ",pos);
    for (i=0;i<setup[1];i++) printf("%02X",anzeige[i]);
    copy64(rel);
    c=getch();
    if (c=='6') pos++;
    if (c=='4') pos--;
    if (c=='+') rel++;
    if (c=='-') rel--;
    if (pos<0) pos=0;
    if (pos>(groesse/setup[1])) pos=groesse/setup[1];
   }
  free(sprite);
 }

void laden (void)
      {
       if ((dz=fopen(d_name,"r+b"))==NULL)
        printf("\nDatei > %s < existiert nicht!",d_name);
       else
        printf("\nDatei > %s < ge”ffnet \n",d_name);

       fseek(dz,(long)0,2);
       groesse=(ftell(dz))/(sizeof(unsigned char));

       sprite = (unsigned char *) calloc(groesse, sizeof(unsigned char));
       if (sprite == NULL)
        {
         printf("\n\nNicht gengend Speicher frei");
         getch();
         free (sprite);
         exit(0);
        }
       fseek(dz, SEEK_SET, 0);
       fread (sprite,sizeof(unsigned char),groesse,dz);
       fclose(dz);
       getch();
      }


void copy64 (BYTE rel)
 {
  int i;
  gotoxy (24,5); printf("0...1...2...3...4...5...6...7...");
  window(22,6,23,22); cprintf("01020304050607080910111213141516");

  window (24,6,39,13);
  for (i=0;i<32;i++) textcolor (buffer[i+rel]), cprintf ("ÛÛÛÛ");

  window (40,6,55,13);
  for (i=32;i<64;i++) textcolor (buffer[i+rel]), cprintf ("ÛÛÛÛ");

  window (24,14,39,22);
  for (i=64;i<96;i++) textcolor (buffer[i+rel]), cprintf ("ÛÛÛÛ");

  window (40,14,55,22);
  for (i=96;i<128;i++) textcolor (buffer[i+rel]), cprintf ("ÛÛÛÛ");


  window (1,1,80,25);
  textcolor(WHITE);
 }
