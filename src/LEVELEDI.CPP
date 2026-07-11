/**************************************************************************/
/* Programmm    : Boulder Dash Level Editor                               */
/* Autor        : Jan Hoppe                                               */
/* Version      : 1.01                                                    */
/* Beschreibung :                                                         */
/**************************************************************************/

/**************************************************************************/
/* INCLUDE Dateien                                                        */
/**************************************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <dos.h>
#include <mem.h>
#include <ctype.h>

/**************************************************************************/
/* Selbstdefinierte Datentypen                                            */
/**************************************************************************/

union REGS inregs, outregs;     //Standartregister
struct SREGS segregs;           //Segmentregister

typedef unsigned char   BYTE;
typedef unsigned int    WORD;
typedef signed char     SBYTE;
typedef signed int      SWORD;

/**************************************************************************/
/* Konstante Variablen                                                    */
/**************************************************************************/

/**************************************************************************/
/* Globale Variablen                                                      */
/**************************************************************************/

BYTE bsp[1000];                      // Bildschirmspeicher

BYTE *buffer[14];
BYTE *maske = bsp;
BYTE grundfarbe[4];

BYTE level_xgr;
BYTE level_ygr;
BYTE level_dnt;
BYTE level_clk;
BYTE bild_xpos;
BYTE bild_ypos;
BYTE mtime;
BYTE pkt_d1;
BYTE pkt_d2;
BYTE game_speed;

WORD Maus_xpos;
WORD Maus_ypos;
WORD Maus_knop;

/************ Funktions spezifische Wariablen *****************************/


        BYTE levelA1[]=  "XF<¼01;8009?DRRR"
                         "00102030405060708090:0;0=000000000000000"
                         "0000000000000000000000000000000000000000"
                         "5555555555555555555555555555555555555555"
                         "5111111011312011111212111111101111211115"
                         "512:211111101111111112311211110111110115"
                         "5111111111101101111121211211111111211115"
                         "5112211111111121111112112111121112111115"
                         "5210011111111122112111111112111111212115"
                         "5111211211111111211111211211111111212215"
                         "5444444444444444444444444444444111211215"
                         "5110111121131011121111111111312011111215"
                         "5111311111211110111111112002113111121115"
                         "5111121121211111111111112212112111111115"
                         "5110111112111111122011111112110113111015"
                         "5112110112100111121231131111211112131215"
                         "5113211111111111112221121111111113111125"
                         "5111111111444444444444444444444444444445"
                         "5111111111110111311112111112111211111115"
                         "52011111111122112111111112111111212011;5"
                         "5121121111111121111121001111311121001115"
                         "5111123110111111112111111212311111121115"
                         "5111011012112122111111111212311111121125"
                         "5131111211111011111111101211211112111215"
                         "5555555555555555555555555555555555555555";


BYTE *level_param = levelA1;
BYTE *level_data1 = levelA1+96;
BYTE *level_data2 = levelA1+16;

/**************************************************************************/
/* Zeiger                                                                 */
/**************************************************************************/

/**************************************************************************/
/* Funktionen (Vordefinition)                                             */
/**************************************************************************/

void level_laden (void);
void BildschirmAufbau(void);
void copy64 (WORD adr, BYTE *buffer);
void BildschirmMaske (void);
void setvgapalette (WORD nr, BYTE g, BYTE r, BYTE b);
void setebene (int enr);
void byte_holen (BYTE *zcopy, BYTE *bzeig, WORD ebene);
void sprite_convert(BYTE sprnr);
void laden(void);
void speichern(void);

#include "leveledt.cpp"
#include "maus.cpp"

/**************************************************************************/
/* Hauptprogramm                                                          */
/**************************************************************************/

void setpixel(WORD x, WORD y, BYTE farbe)
 {
         asm {
             mov ah,0x0C;
             mov al,farbe;
             mov bh,0;
             mov cx,x;
             mov dx,y;
             int 0x10;
            }

 }


void ausgabe(void)
 {
  WORD x=NULL, y=NULL;

  level_laden();
  maus_loeschen();
  BildschirmMaske();
  BildschirmAufbau();
  gotoxy(1,1);  printf("Level laden");
  gotoxy(1,2);  printf("Level speichern");
  gotoxy(1,3);  printf("Neuer Level");
  gotoxy(1,4);  printf("Programm beenden");
  gotoxy(20,1); printf("Diamanten  : %03i",level_dnt);
  gotoxy(40,1); printf("Zeit       : %03i",level_clk);
  gotoxy(60,1); printf("Farbe 1    : %03i",grundfarbe[0]);
  gotoxy(20,2); printf("Farbe 2    : %03i",grundfarbe[1]);
  gotoxy(40,2); printf("Farbe 3    : %03i",grundfarbe[2]);
  gotoxy(60,2); printf("Farbe 4    : %03i",grundfarbe[3]);
  gotoxy(20,3); printf("Startpos X : %03i",bild_xpos);
  gotoxy(40,3); printf("Startpos Y : %03i",bild_ypos);
  gotoxy(60,3); printf("Mauer      : %03i",mtime);
  gotoxy(20,4); printf("Punkte 1   : %03i",pkt_d1);
  gotoxy(40,4); printf("Punkte 2   : %03i",pkt_d2);
  gotoxy(60,4); printf("Game Speed : %03i",game_speed);
  gotoxy(1,5);  printf("                                                                        ");
  gotoxy(1,6);  printf("F1  F2  F3  F4  F5  F6  F7  F8  F9  F10");

  for (x=bild_xpos*16;x<bild_xpos*16+319;x++)
   if (x<641) setpixel(x,bild_ypos*16+128,1);

  for (x=bild_xpos*16;x<bild_xpos*16+319;x++)
   if (x<641) setpixel(x,bild_ypos*16+319,1);

  for (y=bild_ypos*16;y<bild_ypos*16+191;y++)
   if (y<481) setpixel(bild_xpos*16,y+128,1);

  for (y=bild_ypos*16;y<bild_ypos*16+191;y++)
   if (y<481) setpixel(bild_xpos*16+319,y+128,1);


  maus_anzeigen(Maus_xpos,Maus_ypos);
 }

void eingabe (BYTE nr)
 {
  BYTE c1=NULL;
  BYTE c2=NULL;
  BYTE c3=NULL;
  BYTE flag=NULL;
  BYTE ptr=NULL;

  gotoxy(1,5); printf("Bitte eingeben : ");

  if ( (c1=getch()) != 0x0D)
   {
    flag=1;
    printf("%c",c1);
    if ((c2=getch())!=0x0D)
     {
      flag=2;
      printf("%c",c2);
      if ((c3=getche())!=0x0D) flag=3, printf("%c",c3);;
     }
   }
  if (flag==1) ptr= (c1-0x30);
  if (flag==2) ptr= (c1-0x30)*10 + (c2-0x30);
  if (flag==3) ptr= (c1-0x30)*100 + (c2-0x30) * 10 + (c3-0x30);


  if (flag!=0)
   {
    *(level_param+nr+2) = ptr;
     gotoxy(1,5); printf("                        ");
    }
  }

BYTE maus_pruef(WORD x1, WORD y1, WORD x2, WORD y2)
 {
  if ((Maus_xpos>x1)&&(Maus_xpos<x2)&&(Maus_ypos>y1)&&(Maus_ypos<y2))
   return(0xFF);
  else return (0x00);
 }

void menu (void)
 {
  BYTE sprite_wahl=0;
  WORD ptr=NULL;
  WORD i=NULL;
  BYTE c=NULL;

  while (i!=0xFF)
   {
    maus_pos();
    if (kbhit()!=0)
     {
      c=getch();
      if (c==0x00)
       {
        c=getch();
        if ((c>0x3A)&(c<0x45)) sprite_wahl=c-0x3B;
        *(level_data2+39) = sprite_wahl;
        ausgabe();
       }
     }

    if (Maus_knop==0x01)
     {
      if (maus_pruef(0,48,145,63) == 0xFF) i=0xFF;
      if (maus_pruef(0,0,145,15) == 0xFF)
       {
        laden();
        level_laden();
        ausgabe();
       }
      if (maus_pruef(0,16,145,31) == 0xFF)
       {
        speichern();
        level_laden();
        ausgabe();
       }


      // Wertewahl
      if (maus_pruef(146,0,639,63) == 0xFF)
       eingabe(  (Maus_xpos-146)/160 + (Maus_ypos/16) *3 );

      // Sprites wahl

     if (maus_pruef(0,96,416,112) == 0xFF)
      {
       sprite_wahl=Maus_xpos/32;
       if (sprite_wahl==12) sprite_wahl=13;
       *(level_data2+39) = sprite_wahl;
      }

     if (maus_pruef(0,128,639,479) == 0xFF) ptr = Maus_xpos/16 + ((Maus_ypos-128)/16)*40,
       *(level_data1+ptr) = sprite_wahl;

     while (Maus_knop!=0) maus_pos();
     ausgabe();
    }
   }
 }


void main (void)
 {
  WORD i=NULL;
  WORD j=NULL;
  int x,y;

  printf("\n\nProgramm by Jan Hoppe 1999. All rights reserved.\n\n");
  getch();

  inregs.x.ax = 18;                 //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  sprites_laden();
  for (j=0;j<14;j++)
   {
    for (i=0;i<=255;i++)
     {
      if ( *(buffer[j]+i) > 0x2F )
       {
        *(buffer[j]+i) = *(buffer[j]+i) - 0x30;
        if (*(buffer[j]+i)==0x03) *(buffer[j]+i)=0x04;
       }
     }
   }

  for (i=0;i<14;i++) sprite_convert(i);


  maus_ermiteln();

  maus_anzeigen(1,1);

  ausgabe();

  menu();

  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);

  exit(EXIT_SUCCESS);
 }


/****************************** Funktionen ************************/

void BildschirmMaske (void)
 {
  int i,j;
  BYTE *zeiger1 = level_data2;
  BYTE *zeiger2 = bsp;

  for (i=0;i<24;i++)
   {
    memcpy (zeiger2,zeiger1,40);
    zeiger1 = zeiger1 + 40;
    zeiger2 = zeiger2 + 40;
   }
  maske = bsp;
 }

void setebene (int enr)
 {
  asm {
       mov dx,0x03C4;
       mov ax,0x0002;
       out dx,ax;
       mov dx,0x03C5;
       mov ax,enr;
       out dx,ax;
      }
 }

void BildschirmAufbau(void)
 {
  WORD adr1=NULL;
  WORD adr2=NULL;
  maske = bsp;

  for (adr2=7680;adr2<38400;adr2=adr2+1280)
   {
    for (adr1=0;adr1<80;adr1=adr1+2)
     {
      setebene(1); copy64(adr1+adr2,buffer[*maske]);
      setebene(2); copy64(adr1+adr2,buffer[*maske] + 32);
      setebene(4); copy64(adr1+adr2,buffer[*maske] + 64);
      maske++;
     }
   }
 }

void setvgapalette (WORD nr, BYTE g, BYTE r, BYTE b)
 {
  asm {
       mov ah,0x10;
       mov al,0x10;
       mov bx,nr;
       mov ch,g;
       mov cl,r;
       mov dh,b;
       int 0x10;
      }
 }

void setnewpalette (void)
 {
  WORD fn=NULL;
  BYTE f1,f2,f3;

  int palette[]= { 8,8,8, 63,63,63, 46,8,8, 28,63,63, 46,8,46, 8,46,8,
                   8,8,46, 63,63,8, 46,28,8, 36,68,8, 63,28,28, 28,28,28,
                   36,36,36, 28,63,28, 28,28,63, 46,46,46 };

  for (fn=0;fn<4;fn++)
   {
    f1=palette[grundfarbe[fn]*3+1];
    f2=palette[grundfarbe[fn]*3+2];
    f3=palette[grundfarbe[fn]*3];
    setvgapalette(fn,f1,f2,f3);
   }
 }

void copy64 (WORD adr, BYTE *buffer)
 {
        asm  cli;                       //Interrupt sperren
        asm  cld;                       //Richtungsflag l”schen
        asm {
             mov  ax,adr;
             mov  di,ax;                //in DI Register
             mov  ax,0xA000;            //Segment Register mit der
             mov  es,ax;                //Segmentadresse laden
             mov  si,buffer;            //Buffer Anfangsadresse
             mov  dx,0x0010;
             mov  cx,dx;
            }
zeile:;
        asm {
             mov  dx,cx;
             movsw;
             add  di,78;
             mov  cx,dx;
             loop zeile;
            }
       asm   sti;                       //Interrupt erlauben

 }

void level_laden(void)
 {
  BYTE *level = level_param;

  level_xgr = *level; level++;          //Level X-Gr”áe
  level_ygr = *level; level++;          //Level Y-Gr”áe
  level_dnt = *level; level++;          //Minimale Diamanten Anzahl
  level_clk = *level; level++;          //Level Zeit
  grundfarbe[0] = *level; level++;      //Grungfarbe 1
  grundfarbe[1] = *level; level++;      //Grungfarbe 2
  grundfarbe[2] = *level; level++;      //Grungfarbe 3
  grundfarbe[3] = *level; level++;      //Grungfarbe 4
  bild_xpos = *level; level++;          //Levelstart X-Pos.
  bild_ypos = *level; level++;          //Levelstart Y-Pos.
  mtime = *level; level++;              //Umwandlungszeit
  pkt_d1 = *level; level++;             //Punkte pro Diamant 1
  pkt_d2 = *level; level++;             //Punkte pro Diamant 2
  game_speed= *level; level++;

  *(level_data2+0)=0; *(level_data2+8)=4;  *(level_data2+16)=8;
  *(level_data2+2)=1; *(level_data2+10)=5; *(level_data2+18)=9;
  *(level_data2+4)=2; *(level_data2+12)=6; *(level_data2+20)=10;
  *(level_data2+6)=3; *(level_data2+14)=7; *(level_data2+22)=11;
  *(level_data2+24)=13;

  setnewpalette();
 }


/******************************************************/

void byte_holen (BYTE *zcopy, BYTE *bzeig, WORD ebene)
 {
  int i;
  int bit,zs;
  *zcopy=0;

  for (bit=0;bit<8;bit++)
   {
    zs = *bzeig;          //Inhalt laden
    zs = zs >> ebene;     //Ebene laden
    zs = zs & 0x01;       //und ausmaskieren
    zs = zs << 8;         //auf richtige Position verschieben
    zs = zs >> bit+1;
    *zcopy = *zcopy | zs; //und setzen
    bzeig++;
   }
 }

void sprite_convert(BYTE sprnr)
 {
  BYTE copy[128];
  BYTE *zcopy=copy;
  BYTE *bzeig=buffer[sprnr];
  WORD zs1;
  WORD i,ebene;
  for (i=0;i<128;i++) copy[i]=0;

  for (ebene=0;ebene<3;ebene++)
   {
    bzeig=buffer[sprnr];
    for (i=0;i<32;i++)
     {
      byte_holen(zcopy,bzeig,ebene);
      zcopy++;
      bzeig=bzeig+8;
     }
   }
  bzeig=buffer[sprnr];
  zcopy=copy;
  memcpy (bzeig,zcopy,128);
 }

void speichern (void)
      {
       FILE *dz=NULL;
       char *d_name="                            ";
       char c=NULL;
       WORD nr=NULL;
       WORD i=NULL;

       gotoxy(1,5); printf("Dateiname : "); scanf("%s",d_name);
       gotoxy(1,5); printf("                                                               ");
       gotoxy(1,5); printf("Levelnummer : "); scanf("%d",&nr);

       if ((dz=fopen(d_name,"r+b"))==NULL)
	{
	 gotoxy(1,5); printf("\nDatei > %s < existiert nicht! Anlegen?",d_name);
	 c=getch();
	 c=toupper(c);
	 if (c=='J') dz=fopen(d_name,"w+b");
	 if (c=='N') goto ende;
	}

       fseek(dz,(40*22+18)*nr,0);
       fwrite(level_param, sizeof(BYTE), 16, dz);
       fwrite(level_data1, sizeof(BYTE), 40*22, dz);

       ende:;
       fclose(dz);
      }


void laden (void)
      {
       FILE *dz=NULL;
       char *d_name="                            ";
       char c=NULL;
       WORD nr=NULL;
       WORD i=NULL;

       gotoxy(1,5); printf("Dateiname : "); scanf("%s",d_name);
       gotoxy(1,5); printf("                                                               ");
       gotoxy(1,5); printf("Levelnummer : "); scanf("%d",&nr);

       if ((dz=fopen(d_name,"r+b"))==NULL)
	{
	 gotoxy(1,5); printf("Datei > %s < existiert nicht! Anlegen?",d_name);
	 c=getch();
	 c=toupper(c);
	 if (c=='J') fopen(d_name,"w+b");
	 if (c=='N') goto ende;
	}

       fseek(dz,(40*22+18)*nr,0);
       fread(level_param, sizeof(BYTE), 16, dz);
       fread(level_data1, sizeof(BYTE), 40*22, dz);

       ende:;
       fclose(dz);
      }



