/**************************************************************************/
/* Programmm    : Boulder Dash                                            */
/* Autor        : Jan Hoppe                                               */
/* Version      : 1.02                                                    */
/* Beschreibung :                                                         */
/* Speichermodel: LARGE                                                   */
/**************************************************************************/

/**************************************************************************/
/* INCLUDE Dateien                                                        */
/**************************************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <process.h>
#include <string.h>
#include <alloc.h>
#include <dos.h>

//extern "C" void LAUT_AUS(int,char*,int,int);

/**************************************************************************/
/* Konstante Variablen                                                    */
/**************************************************************************/

#define F1             0x3B
#define F2             0x3C
#define F3             0x3D
#define F4             0x3E
#define OBEN           0x48
#define UNTEN          0x50
#define RECHTS         0x4B
#define LINKS          0x4D
#define SPACE          0x20

#define INT_TIMER      0X1C    //Timer Interrupt
#define __CPPARGS ...

#define MASK_LEER      0
#define MASK_ERDE      1
#define MASK_STEIN     2
#define MASK_DIAMANT   3
#define MASK_MAUER     4
#define MASK_STAHL     5
#define MASK_ROCK      6
#define MASK_LAVA      7
#define MASK_GEIST     8
#define MASK_MOTYL     9
#define MASK_HAUSE     10
#define MASK_HAUSA     11
#define MASK_EXPLO     12
#define MASK_MAUERU    13
#define MASK_EXPLO2    14
#define MASK_SLAUF     15

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
/* Globale Variablen                                                      */
/**************************************************************************/

BYTE bsp[240];                     // Bildschirmspeicher
BYTE newmask[240];
BYTE level_kopie[1024];

SWORD bild_xpos=0, bild_ypos=0;
BYTE  level_xgr=0, level_ygr=0;

BYTE *buffer[16];
BYTE *s_zeiger[49];                  //SPRITE Zeiger (Orginal)
BYTE *z_zeiger[90];                  //SPRITE Zeiger (Geordnet)
BYTE *f_zeiger;                      //DEMO  Zeiger
BYTE *e_zeiger=NULL;                 //SOUND Zeiger
BYTE *maske = bsp;
BYTE *level = NULL;
BYTE grundfarbe[4];
BYTE blitz  = NULL;
BYTE kop    = NULL;
WORD flags  = NULL;

BYTE diamanten  = NULL;
BYTE level_ende = NULL;
BYTE level_next = NULL;
WORD score      = NULL;
BYTE leben      = 3;
BYTE level_dnt=0, level_clk=0;
BYTE mtime      = NULL;
BYTE pkt_d1     = NULL;
BYTE pkt_d2     = NULL;
BYTE game_speed = NULL;

BYTE clk_1 = NULL;
BYTE clk_2 = NULL;
BYTE clk_4 = NULL;
BYTE clk_18= NULL;

BYTE mrun  = NULL;
WORD start = NULL;
WORD stop  = NULL;
BYTE stat  = NULL;

char relx  = NULL;
char rely  = NULL;

/************ Funktions spezifische Wariablen *****************************/

BYTE *menutext="                                        ";
WORD menuzeit=0;

signed char richtung        = NULL;

BYTE *d_name="level.bin";
BYTE *s_name="sprites.bin";
BYTE *f_name="demo.bin";
BYTE *e_name="sound.bin";

BYTE status          = NULL;
BYTE wechsel_vier    = NULL;
BYTE wechsel_boulder = NULL;
BYTE wechsel_explo   = NULL;

BYTE wechsel_haus    = NULL;
BYTE anfang_var      = NULL;
BYTE rock_flag       = NULL;
BYTE sound_var       = NULL;
SBYTE cavenr=0;
SBYTE levelnr=1;
BYTE pause           = NULL;

/**************************************************************************/
/* Zeiger                                                                 */
/**************************************************************************/

#include "intro.cpp"

void interrupt (*alte_timer_isr)(__CPPARGS);     //Alte Timer ISR

/**************************************************************************/
/* Funktionen (Vordefinition)                                             */
/**************************************************************************/

void interrupt timer_interrupt_service_routine (__CPPARGS);
void interrupt menu_interrupt_service_routine (__CPPARGS);

void level_laden (BYTE level_nr);
void copy64 (void);
void BildschirmMaske (SWORD bild_xpos, SWORD bild_ypos);
//void boulder_wait   (void);
void boulder_lauf (void);
void sprite_explo (void);
void regel (void);
void anfang (void);
void ende (void);
void explosion_diamant(void);
BYTE tastatur_code(void);
void hauptmenu(void);
void game_start(void);
void demo (BYTE nr);
void setvgapalette (WORD nr, BYTE g, BYTE r, BYTE b);
void setnewpalette(void);
void sprites_wechsel(void);
void level_in (void);
void level_out(void);

/**************************************************************************/
/* Hauptprogramm                                                          */
/**************************************************************************/

void error (BYTE *text)
 {
  WORD i=NULL;

  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);

  for (i=0;i<40;i++) farfree(s_zeiger[i]);
  farfree (e_zeiger);
  fcloseall();

  clrscr();
  printf("%s \n",text);
  printf("Taste drcken um zu beenden!");
  getch();
  exit(0);
 }

void main (void)
 {
  BYTE i=0;
  BYTE s_flag=0;
  FILE *dz;

  intro();

  inregs.x.ax = 0x0013;                 //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  textcolor(0);
  textbackground(1);

  z_zeiger[0]=s_zeiger[0];                               //Leer
  z_zeiger[1]=s_zeiger[1];                               //Erde
  z_zeiger[2]=s_zeiger[2];                               //Stein
  z_zeiger[3]=s_zeiger[3]; z_zeiger[4]=s_zeiger[4];      //Diamant
  z_zeiger[5]=s_zeiger[5]; z_zeiger[6]=s_zeiger[6];
  z_zeiger[7]=s_zeiger[7]; z_zeiger[8]=s_zeiger[8];
  z_zeiger[9]=s_zeiger[9]; z_zeiger[10]=s_zeiger[10];
  z_zeiger[11]=s_zeiger[11];                             //Mauer
  z_zeiger[12]=s_zeiger[12];                             //Stahl
  z_zeiger[13]=s_zeiger[13]; z_zeiger[14]=s_zeiger[14];  //Rock
  z_zeiger[15]=s_zeiger[15]; z_zeiger[16]=s_zeiger[16];
  z_zeiger[17]=s_zeiger[17]; z_zeiger[18]=s_zeiger[18];
  z_zeiger[19]=s_zeiger[19]; z_zeiger[20]=s_zeiger[20];
  z_zeiger[21]=s_zeiger[21]; z_zeiger[22]=s_zeiger[22];
  z_zeiger[23]=s_zeiger[23];
  z_zeiger[24]=s_zeiger[24]; z_zeiger[25]=s_zeiger[25];      //Lava
  z_zeiger[26]=s_zeiger[26]; z_zeiger[27]=s_zeiger[27];
  z_zeiger[28]=s_zeiger[24]; z_zeiger[29]=s_zeiger[25];
  z_zeiger[30]=s_zeiger[26]; z_zeiger[31]=s_zeiger[27];
  z_zeiger[32]=s_zeiger[28]; z_zeiger[33]=s_zeiger[28];      //Geist
  z_zeiger[34]=s_zeiger[29]; z_zeiger[35]=s_zeiger[29];
  z_zeiger[36]=s_zeiger[30]; z_zeiger[37]=s_zeiger[30];
  z_zeiger[38]=s_zeiger[31]; z_zeiger[39]=s_zeiger[31];
  z_zeiger[40]=s_zeiger[32]; z_zeiger[41]=s_zeiger[32];      //Motyl
  z_zeiger[42]=s_zeiger[33]; z_zeiger[43]=s_zeiger[34];
  z_zeiger[44]=s_zeiger[34]; z_zeiger[45]=s_zeiger[33];
  z_zeiger[46]=s_zeiger[32]; z_zeiger[47]=s_zeiger[32];
  z_zeiger[48]=s_zeiger[12]; z_zeiger[49]=s_zeiger[35];      //Haus ein
  z_zeiger[50]=s_zeiger[12]; z_zeiger[51]=s_zeiger[35];      //Haus aus
  z_zeiger[52]=s_zeiger[36]; z_zeiger[53]=s_zeiger[36];      //Explo1
  z_zeiger[54]=s_zeiger[37]; z_zeiger[55]=s_zeiger[38];
  z_zeiger[56]=s_zeiger[38]; z_zeiger[57]=s_zeiger[37];
  z_zeiger[58]=s_zeiger[36]; z_zeiger[59]=s_zeiger[36];
  z_zeiger[60]=s_zeiger[39]; z_zeiger[61]=s_zeiger[40];      //Maueru
  z_zeiger[62]=s_zeiger[41]; z_zeiger[63]=s_zeiger[42];
  z_zeiger[64]=s_zeiger[39]; z_zeiger[65]=s_zeiger[40];
  z_zeiger[66]=s_zeiger[41]; z_zeiger[67]=s_zeiger[42];
  z_zeiger[68]=s_zeiger[43]; z_zeiger[69]=s_zeiger[44];      //Explo2
  z_zeiger[70]=s_zeiger[44]; z_zeiger[71]=s_zeiger[45];
  z_zeiger[72]=s_zeiger[46]; z_zeiger[73]=s_zeiger[46];
  z_zeiger[74]=s_zeiger[47]; z_zeiger[75]=s_zeiger[47];
  z_zeiger[76]=s_zeiger[48];                                 //Stahl lauf

  buffer[MASK_LEER]    =z_zeiger[0];
  buffer[MASK_ERDE]    =z_zeiger[1];
  buffer[MASK_STEIN]   =z_zeiger[2];
  buffer[MASK_DIAMANT] =z_zeiger[3];
  buffer[MASK_MAUER]   =z_zeiger[11];
  buffer[MASK_STAHL]   =z_zeiger[12];
  buffer[MASK_ROCK]    =z_zeiger[13];
  buffer[MASK_LAVA]    =z_zeiger[24];
  buffer[MASK_GEIST]   =z_zeiger[32];
  buffer[MASK_MOTYL]   =z_zeiger[40];
  buffer[MASK_HAUSE]   =z_zeiger[48];
  buffer[MASK_HAUSA]   =z_zeiger[50];
  buffer[MASK_EXPLO]   =z_zeiger[52];
  buffer[MASK_MAUERU]  =z_zeiger[11];
  buffer[MASK_EXPLO2]  =z_zeiger[68];
  buffer[MASK_SLAUF]   =z_zeiger[76];

  hauptmenu();

  for (i=0;i<40;i++) farfree(s_zeiger[i]);
  farfree(e_zeiger);

  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);

  exit(EXIT_SUCCESS);
 }

void menuausgabe (void)
 {
   grundfarbe[0]=0;
   grundfarbe[1]=1;
   grundfarbe[2]=6;
   grundfarbe[3]=2;
   setnewpalette();

   WORD i=NULL;

   BYTE menu[]="44444444444444444444"
               "45555555555555555554"
               "45555555555555555554"
               "45555555555555555554"
               "45555555555555555554"
               "45555555555555555554"
               "44444444444444444444"
               "00000000000000000000"
               "00000000000000000000"
               "00000000000000000000"
               "00000000000000000000"
               "00000000000000000000";

  for (i=0;i<240;i++) menu[i]=menu[i]-0x30;

  maske=menu;
  copy64();
  gotoxy(1,1) ; printf("                                        ");
  gotoxy(10,5); printf("ÚÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ¿");
  gotoxy(10,6); printf("³ Boulder Dash v1.1 ³");
  gotoxy(10,7); printf("ÀÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÙ");
  gotoxy(1,16); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
  gotoxy(1,17); printf("      Copyright 1999 by Jan Hoppe       ");
  gotoxy(1,18); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
  gotoxy(1,19); printf(" F1 - Spiel starten  F2 - Demo          ");
  gotoxy(1,21); printf(" F3 - Hilfe          F4 - Ende          ");
  gotoxy(1,22); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
 }

void interrupt menu_interrupt_service_routine (__CPPARGS)
 {
  WORD i=NULL;

  BYTE *z1="                                        Boulde Dash v1.1                        "
           "Copyright by Jan Hoppe 1999             "
           "Drcken Sie bitte die F1 Taste um das Sp"
           "iel zu starten                          "
           "                                        ";
  if (clk_18++>1)
   {
    clk_18=0;
    if (menuzeit>(i-40)) menuzeit=0;
    _fmemcpy(menutext,z1+menuzeit,40);
    menuzeit++;
   }
 }

void hauptmenu(void)
 {
  BYTE c=1;

  alte_timer_isr = getvect(INT_TIMER);             //Alten Interrupt sichern
  setvect(INT_TIMER, menu_interrupt_service_routine);  //neuen Installieren

  menuausgabe();

  while (c!=F4)
   {

    gotoxy(1,17); printf("%s",menutext);
    gotoxy(1,23); printf("     LEVEL : %02i       CAVE : %c         ",levelnr,cavenr+'A'-((cavenr-1)/4));
    if (kbhit()!=0) c=getch();
    if (c==0)
     {
      c=getch();
      switch(c)
       {
        default    : break;
        case OBEN  : if (levelnr++>3)  levelnr = 4;  break;
        case UNTEN : if (levelnr--<2)  levelnr = 1;  break;
        case LINKS : cavenr=cavenr+1;
                     if (cavenr>15) cavenr=15;
                     break;
        case RECHTS: cavenr=cavenr-1;
                     if (cavenr<0)  cavenr=0;
                     break;
        case F1    : setvect(INT_TIMER, alte_timer_isr);
                     leben=3;
                     level_next=0;
                     level_ende=0xFF;
                     while (level_ende==0xFF)
                      {
                       level_laden(cavenr=cavenr+level_next);
                       game_start();
                       delay(500);
                       level_out();
                       if (level_next==21) level_next=0;
                       if (leben<1) break;
                       if (stat!=0) level_ende=0xFF;
                      }
                     menuausgabe();
                     alte_timer_isr = getvect(INT_TIMER);
                     setvect(INT_TIMER, menu_interrupt_service_routine);
                     break;
        case F2   :  setvect(INT_TIMER, alte_timer_isr);
                     level_laden(0);
                     demo (0);
                     level_out();
                     menuausgabe();
                     alte_timer_isr = getvect(INT_TIMER);
                     setvect(INT_TIMER, menu_interrupt_service_routine);
                     break;
       }
     }
   }
  setvect(INT_TIMER, alte_timer_isr);        //Alten Interrupt wiederherstellen
 }

void demo(BYTE nr)
 {
  FILE *dz;

  f_zeiger = (BYTE *) calloc(256, sizeof(BYTE));
  if (f_zeiger == NULL)
    {
     printf("Speicherfehler"), getch(), exit(0);
    }
  if ((dz=fopen(f_name,"r+b"))==NULL) exit(0);

  fread(f_zeiger,sizeof(BYTE),256,dz);
  fclose(dz);

  BYTE c=NULL;
  BYTE r=NULL;
  BYTE pr=NULL;
  WORD speed = game_speed;

  speed = 59659 / speed;
  BYTE zeit_low = speed & 0x00FF;
  BYTE zeit_high= ( speed >> 8 ) & 0x00FF;

  outp(0x43,0x34);
  outp(0x40,zeit_low);          //low
  outp(0x40,zeit_high);         //high

  alte_timer_isr = getvect(INT_TIMER);             //Alten Interrupt sichern
  setvect(INT_TIMER, timer_interrupt_service_routine);  //neuen Installieren

  BYTE *demo_zeiger=f_zeiger;

  delay(7000);

  while (r!='1')
   {
    if (kbhit()!=0) c=getch();
    r=*demo_zeiger;
    if ((clk_1==0)&&(pr==0)) pr=1, demo_zeiger++;
    if ((clk_1==1)&&(pr==1)) pr=0;

    switch(r)
     {
      case 0x1D : kop=6; break;
      case 0x9D : kop=0; break;
      case 0x4D : richtung=1;              flags=0x40; status=0; break;
      case 0x4B : richtung= (-1);          flags=0x10; status=1; break;
      case 0x50 : richtung=level_xgr;      flags=0x20;           break;
      case 0x48 : richtung=(-1)*level_xgr; flags=0x80;           break;
      case 0xCD : flags=flags&0xBF; break;
      case 0xCB : flags=flags&0xEF; break;
      case 0xD0 : flags=flags&0xDF; break;
      case 0xC8 : flags=flags&0x7F; break;
      default   : break;
     }
    if (flags<0x10) flags=0, richtung=0;
    if (level_ende==0xFF) break;
    if (stat!=0) break;

    if (anfang_var<99) gotoxy(1,1), printf("  P L A Y E R   1 ,   %01i  M E N   %c / %i",leben,cavenr+'A'-((cavenr-1)/4),levelnr);
      else gotoxy(1,1), printf("%02i  -  %02i     %02i      %03i         %06i",level_dnt,pkt_d1,diamanten,level_clk,score);
   }

  if ((level_ende==0xFF)&&(stat==0))
   {
   for (level_clk;level_clk>0;level_clk--)
    {
     score++;
     gotoxy(1,1); printf("%02i  -  %02i     %02i      %03i         %06i",level_dnt,pkt_d1,diamanten,level_clk-1,score);
     delay(20);
    }
   delay(1000);
   }
  setvect(INT_TIMER, alte_timer_isr);        //Alten Interrupt wiederherstellen

 }

void game_start(void)
 {
  BYTE c=NULL;
  BYTE l=NULL;
  WORD speed = game_speed;

  speed = 59659 / speed;
  BYTE zeit_low = speed & 0x00FF;
  BYTE zeit_high= ( speed >> 8 ) & 0x00FF;

  outp(0x43,0x34);
  outp(0x40,zeit_low);          //low
  outp(0x40,zeit_high);         //high

  outp  (0x21,0x02);     //IRQ 1 sperren

  outp (0x60,0xED);      //NUM-LOCK OFF

  warten:;               //Warte bis Tastatur bereit
  asm in      al,0x64;
  asm test    al,0x02;
  asm jnz     warten;

  outp (0x60,0x00);

  alte_timer_isr = getvect(INT_TIMER);             //Alten Interrupt sichern
  setvect(INT_TIMER, timer_interrupt_service_routine);  //neuen Installieren

  while (c!=0x01)
   {
    c=tastatur_code();
    switch(c)
     {
      case 0x20 : if (pause==0) pause=1;
                  else pause=0;
                  break;
      case 0x1D : kop=6; break;
      case 0x9D : kop=0; break;
      case 0x4D : richtung=1;              flags=0x40; status=0; break;
      case 0x4B : richtung= (-1);          flags=0x10; status=1; break;
      case 0x50 : richtung=level_xgr;      flags=0x20;           break;
      case 0x48 : richtung=(-1)*level_xgr; flags=0x80;           break;
      case 0xCD : flags=flags&0xBF; break;
      case 0xCB : flags=flags&0xEF; break;
      case 0xD0 : flags=flags&0xDF; break;
      case 0xC8 : flags=flags&0x7F; break;
      default   : break;
     }
    if (flags<0x10) flags=0, richtung=0;
    if (level_ende==0xFF) break;
    if (stat!=0) break;

    if (pause==1) gotoxy(1,1), printf("      N O   O N E   I S   W A I T I N G ");
    else if (anfang_var<99) gotoxy(1,1), printf("  P L A Y E R   1 ,   %01i  M E N   %c / %i",leben,cavenr+'A'-((cavenr-1)/4),levelnr);
         else gotoxy(1,1), printf("%02i  -  %02i     %02i      %03i         %06i",level_dnt,pkt_d1,diamanten,level_clk,score);
   }
  outp (0x21,0x02);                     //IRQ 1 sperren
  outp (0x60,0xF6);                     //Tastaturbefehl Standartwerte*/
  outp (0x21,0x00);                     //IRQ 1 freigeben

  if ((level_ende==0xFF)&&(stat==0))
   for (level_clk;level_clk>0;level_clk--)
    {
     score++;
     gotoxy(1,1); printf("%02i  -  %02i     %02i      %03i         %06i",level_dnt,pkt_d1,diamanten,level_clk-1,score);
     delay(20);
    }

  if (stat!=0)
   {
    leben--;
    if (leben==0)
     {
      delay(1000);
      gotoxy(1,1);
      printf("  G    A    M    E       O   V   E   R  ");
     }
    getch();
   }
  setvect(INT_TIMER, alte_timer_isr);        //Alten Interrupt wiederherstellen

  outp(0x43,0x34);
  outp(0x40,0x00);
  outp(0x40,0x00);
 }

/****************************** Funktionen ************************/

void interrupt timer_interrupt_service_routine (__CPPARGS)
 {
    if (clk_1++>1)   clk_1=0;
    if (clk_2++>2)   clk_2=0;
    if (clk_4++>4)   clk_4=0;
    if (clk_18++>20) clk_18=0;

    if (relx>0) relx--, bild_xpos++;
    if (relx<0) relx++, bild_xpos--;
    if (rely>0) rely--, bild_ypos++;
    if (rely<0) rely++, bild_ypos--;

    if (bild_xpos>level_xgr-20) bild_xpos=level_xgr-20;
    if (bild_xpos<0) bild_xpos=0;
    if (bild_ypos>level_ygr-12) bild_ypos=level_ygr-12;
    if (bild_ypos<0) bild_ypos=0;

    boulder_lauf();
    sprites_wechsel();

    if (pause==0)
     {
      if ((clk_18==0)&&(level_ende!=0xFF))   //Level Zeit
       {
        if (anfang_var>99) level_clk--;      //Level Zeit-- wenn augebaut
        if (mrun==1) mtime--;                //Mauer Zeit-- wenn aktiv
       }

      if (mtime==0) mrun=0;                 //Mauer inaktiv wenn Mauerzeit=0
      if (level_clk==0) level_ende=0xFF;    //Level Ende wenn Zeit=0

      if (level_ende!=0xFF)                 //Kein Level Ende
       {
        if ((clk_1==0)&&(anfang_var>65)) regel();  //Regel
        if (anfang_var<101) anfang();              //Level aufbau
        if (diamanten>=level_dnt) ende();          //Level ende
       }
     }

    BildschirmMaske(bild_xpos,bild_ypos);   //Bildschirmmaske
    maske=bsp;
    if ((anfang_var<65)&&(level_ende!=0xFF)) level_in();
    copy64();                              //Bildschirm aufbauen
 }

void level_out (void)
 {
  WORD zaehler=0;
  for (zaehler=0;zaehler<21;zaehler++)
    movedata(0xA000,zaehler*320, 0xA000,0, 64320-zaehler*320);
 }

void level_in (void)
 {
  WORD i=NULL, j=NULL, z=NULL;
  BYTE *mz=maske;
  if (anfang_var<5) for (i=0;i<240;i++) newmask[i]=15;
  else
   {
    for (z=0;z<4;z++)
     {
      i=0;
      while (newmask[j]==0)
       {
        j=(rand()%240)+1;
        if (i++>240) break;
       }
      newmask[j]=0;
     }
   }
  for (i=0;i<240;i++)
   if (newmask[i]==15) *(mz+i) = 15;
 }

void sprites_wechsel(void)
 {
  if (wechsel_vier++>6) wechsel_vier=0;
  if (mrun==1) buffer[MASK_MAUERU] = z_zeiger[60+wechsel_vier];
  else buffer[MASK_MAUERU] = z_zeiger[11];
  buffer[MASK_DIAMANT]=z_zeiger[3+wechsel_vier];
  buffer[MASK_LAVA]   =z_zeiger[24+wechsel_vier];
  buffer[MASK_GEIST]  =z_zeiger[32+wechsel_vier];
  buffer[MASK_MOTYL]  =z_zeiger[40+wechsel_vier];
  buffer[MASK_EXPLO]  =z_zeiger[52+wechsel_explo];
  buffer[MASK_EXPLO2] =z_zeiger[68+wechsel_explo];
  buffer[MASK_SLAUF]  =z_zeiger[76]+wechsel_vier*16;
  if (wechsel_explo>0) wechsel_explo++;
  if (wechsel_explo==8) wechsel_explo=7;
 }

void anfang (void)
 {
  if (anfang_var<92)
   {
    if (clk_4<3) buffer[MASK_HAUSE]=z_zeiger[49];
    else buffer[MASK_HAUSE]=z_zeiger[48];
   }
  if (anfang_var==92) *(level+start)=12, wechsel_explo=1;
  if (anfang_var==99) *(level+start)=6;
  anfang_var++;
 }

void ende (void)
 {
  if (blitz==0) setvgapalette(0,63,63,63), blitz=1;
  else setvgapalette (0,8,8,8);
  if (clk_4<3) buffer[MASK_HAUSA]=z_zeiger[49];
  else buffer[MASK_HAUSA]=z_zeiger[48];
 }

void BildschirmMaske (SWORD bild_xpos, SWORD bild_ypos)
 {
  int i,j;
  BYTE *zeiger1 = level;
  BYTE *zeiger2 = bsp;

  zeiger1 = zeiger1 + ((bild_ypos)*level_xgr) + bild_xpos;

  for (i=0;i<12;i++)
   {
    _fmemcpy (zeiger2,zeiger1,20);
    zeiger1 = zeiger1 + level_xgr;
    zeiger2 = zeiger2 + 20;
   }
 }

void explosion (BYTE anim, BYTE *zeiger_1)
 {
  if ( *(zeiger_1-level_xgr-1) != 5 ) *(zeiger_1-level_xgr-1)=anim;
  if ( *(zeiger_1-level_xgr) != 5 ) *(zeiger_1-level_xgr)=anim;
  if ( *(zeiger_1-level_xgr+1) != 5 ) *(zeiger_1-level_xgr+1)=anim;
  if ( *(zeiger_1-1) != 5 ) *(zeiger_1-1)=anim;
  if (*zeiger_1 != 5) *zeiger_1=anim;
  if (*(zeiger_1+1) != 5) *(zeiger_1+1)=anim;
  if ( *(zeiger_1+level_xgr-1) != 5 ) *(zeiger_1+level_xgr-1)=anim;
  if ( *(zeiger_1+level_xgr) != 5 ) *(zeiger_1+level_xgr)=anim;
  if ( *(zeiger_1+level_xgr+1) != 5 ) *(zeiger_1+level_xgr+1)=anim;
  wechsel_explo=1;
 }

void regel (void)
 {
  WORD i=0;
  BYTE lava_var=0;
  BYTE lava_nr=(rand()%96)+1;
  BYTE j=0,w=0;
  BYTE *ptr = level;
  BYTE lf=3;

  if ((anfang_var>100)&&(stat==0)) stat=1;

  for (j=0;j<level_ygr;j++)
   {
    for (i=0;i<level_xgr;i++)
     {
  /************* Explosion -> Diamant ******************************/
  /************* Explosion -> Leer *********************************/
      if (((*ptr&0x1F)==12) && (wechsel_explo==7)) *ptr = 0x80;
      if (((*ptr&0x1F)==14) && (wechsel_explo==7)) *ptr = 0x83;
  /************* Lava **********************************************/
      if ((*ptr&0x9F)==0x07)
       {
        lava_var++;
        if (lava_var==lava_nr)
         {
          if ( (*(ptr-level_xgr)&0xFE)==0 ) *(ptr-level_xgr)=0x87;
          else if ( (*(ptr+level_xgr)&0xFE)==0 ) *(ptr+level_xgr)=0x87;
          else if ( (*(ptr-1)&0xFE)==0 ) *(ptr-1)=0x87;
          else if ( (*(ptr+1)&0xFE)==0 ) *(ptr+1)=0x87;
         }
       }

  /************* Schmeterling **************************************/
      if ( ((*ptr&0x8F)==9) )
       {
        // Lava | Rockford //
        if ( (*(ptr-level_xgr)&0xFE)==6 ) explosion(0xCE,ptr);
        else if ( (*(ptr+level_xgr)&0xFE)==6 ) explosion(0xCE,ptr);
        else if ( (*(ptr-1)&0xFE)==6 ) explosion(0xCE,ptr);
        else if ( (*(ptr+1)&0xFE)==6 ) explosion(0xCE,ptr);
        else if ( (*(ptr-level_xgr)&0x4C)==0x40) explosion(0xCE,ptr);
        else
         {
          for (w=0;w<4;w++)
           {
            switch (*ptr&0x60)
             {
              case 0x00 : if (*(ptr-level_xgr)==0) *ptr=(*ptr&0x99)|0xA9, w=5;
                          else if (*(ptr-1)==0) w=5;
                          else *ptr=0x39; break;
              case 0x20 : if (*(ptr+1)==0) *ptr=(*ptr&0x99)|0xC9, w=5;
                          else if (*(ptr-level_xgr)==0) w=5;
                          else *ptr=0x59; break;
              case 0x40 : if (*(ptr+level_xgr)==0) *ptr=(*ptr&0x99)|0xE9, w=5;
                          else if (*(ptr+1)==0) w=5;
                          else *ptr=0x79; break;
              case 0x60 : if (*(ptr-1)==0) *ptr=(*ptr&0x99)|0x89, w=5;
                          else if (*(ptr+level_xgr)==0) w=5;
                          else *ptr=0x19; break;
              default   : break;
            }
           }
          switch (*ptr&0x70)
           {
            case 0x00 : *(ptr-1) = 0x89; *ptr=0; break;
            case 0x20 : *(ptr-level_xgr) = 0xA9; *ptr=0; break;
            case 0x40 : *(ptr+1) = 0xC9; *ptr=0; break;
            case 0x60 : *(ptr+level_xgr) = 0xE9;*ptr=0; break;
            default   : break;
           }
          *ptr=*ptr|0x80;
          *ptr=*ptr&0xEF;
         }
       }

  /************* Geist *********************************************/
      if ( ((*ptr&0x8F)==8) )
       {
        if ( (*(ptr-level_xgr)&0x7E)==6 ) explosion(0xCC,ptr);
        else if ( (*(ptr+level_xgr)&0x7E)==6 ) explosion(0xCC,ptr);
        else if ( (*(ptr-1)&0x7E)==6 ) explosion(0xCC,ptr);
        else if ( (*(ptr+1)&0x7E)==6 ) explosion(0xCC,ptr);
        else if ( (*(ptr-level_xgr)&0x42)==0x42  ) explosion(0xCC,ptr);
        else
         {
          for (w=0;w<4;w++)
           {
            switch (*ptr&0x60)
             {
              case 0x00 : if (*(ptr+level_xgr)==0) *ptr=(*ptr&0x99)|0xE8, w=5;
                          else if (*(ptr-1)==0) w=5;
                          else *ptr=0x38; break;
              case 0x20 : if (*(ptr-1)==0) *ptr=(*ptr&0x99)|0x88, w=5;
                          else if (*(ptr-level_xgr)==0) w=5;
                          else *ptr=0x58; break;
              case 0x40 : if (*(ptr-level_xgr)==0) *ptr=(*ptr&0x99)|0xA8, w=5;
                          else if (*(ptr+1)==0) w=5;
                          else *ptr=0x78; break;
              case 0x60 : if (*(ptr+1)==0) *ptr=(*ptr&0x99)|0xC8, w=5;
                          else if (*(ptr+level_xgr)==0) w=5;
                          else *ptr=0x18; break;
              default   : break;
            }
           }
          switch (*ptr&0x70)
           {
            case 0x00 : *(ptr-1) = 0x88; *ptr=0; break;
            case 0x20 : *(ptr-level_xgr) = 0xA8; *ptr=0; break;
            case 0x40 : *(ptr+1) = 0xC8; *ptr=0; break;
            case 0x60 : *(ptr+level_xgr) = 0xE8;*ptr=0; break;
            default   : break;
           }
          *ptr=*ptr|0x80;
          *ptr=*ptr&0xEF;
         }
     }
  /************* Stein, Diamant ****************************************/
      if (((*ptr&0x9F) == 3) || ((*ptr&0x9F) == 2))  //Stein oder Diamant?
       {
        switch ( *(ptr+level_xgr)&0x0F )         //Was ist drunter?
         {
          default: *ptr = *ptr&0x1F;
          case 9 :
          case 8 : break;
          case 0 : *(ptr+level_xgr) = *ptr|0xC0;  //nichts?
                     *ptr = 0x80;
                     break;
          case 2 :                                     //Stein?
          case 3 :                                     //Diamant? oder Mauer?
          case 4 : if ((*(ptr-1)== 0) && (*(ptr-1+level_xgr)==0))
                    {
                     *(ptr-1) = *ptr|0x80;
                     *ptr = 0x80;
                     *(ptr-1+level_xgr)= 0x80;
                    }
                   else
                   if ( ( *(ptr+1)==0) && (*(ptr+1+level_xgr)==0))
                    {
                     *(ptr+1) = *ptr|0x80;
                     *ptr = 0x80;
                     *(ptr+1+level_xgr)= 0x80;
                    }

                     else *ptr = *ptr&0x1F;
                     break;
          case 13 : if (mtime>0)
                     {
                      if ((*ptr&0x40)==0x40)
                       {
                        mrun=1;
                        if (*(ptr+2*level_xgr)==0)
                         *(ptr+2*level_xgr) = (*ptr - 1)|0xC2;
                        *ptr=0x80;
                       }

                     }
                    else if ((*ptr&0x40)==0x40) *ptr=0;
                    break;
          case 6  : if (((*(ptr+level_xgr)&0x06)==0x06)&&
                      ((*ptr&0x4C)==0x40)) explosion(0x8C,ptr+level_xgr);
                    break;
           }
         }

  /************* Boulder ****************************************/
    if ((*ptr&0x9F) == 6)
       {
        stat=0;
        if ((bild_xpos+17<i)&&(bild_xpos<level_xgr-20)) relx=7;
        if ((bild_xpos+1==i)&&(bild_xpos>0)) relx=-7;
        if ((bild_ypos+9<j)&&(bild_ypos<level_ygr-12)) rely=5;
        if ((bild_ypos+1==j)&&(bild_ypos>0)) rely=-5;
        else
        switch ( *(ptr+richtung)&0x9F)
         {
          default: break;
          case 11: if (diamanten<level_dnt) break;
                   level_ende=0xFF;
                   level_next++;
                   anfang_var=0;
          case 3 : diamanten++;
                   if (diamanten>=level_dnt) pkt_d1=pkt_d2;
                   score=score+pkt_d1;
          case 0 :
          case 1 : *(ptr+richtung) = 0x86 ^ kop;
                   *ptr=0x80 ^ kop;
                   break;
          case 2 : if  ((*(ptr+(richtung*2))==0)&&(clk_4==0))
                      {
                       if ((richtung==1)||(richtung==-1))
                        {
                         *(ptr+richtung) = 0x86 ^ kop;
                         *(ptr+(2*richtung))= 0x82;
                         *ptr=0x80 ^ kop;
                       }
                      }
                     break;
         }
       }
      ptr++;
     }
   }

  ptr=level;

  for (i=0;i<level_xgr*level_ygr;i++)
   {
    *ptr=*ptr&0x7F;
    ptr++;
   }
  ptr=level;

  for (i=0;i<level_xgr*level_ygr;i++)
   {
    if (*ptr==7)
     {
      if ( ((*(ptr-level_xgr)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(ptr+level_xgr)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(ptr-1)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(ptr+1)&0xFE)|1)==1 ) lf=2;
     }
    ptr++;
   }

 ptr=level;

  if ((lava_var>95)||(lf==3))
   for (i=0;i<level_xgr*level_ygr;i++)
    {
     if (*ptr==7) *ptr=lf;
     ptr++;
    }

  ptr=level;

 }


/******************* Fertige Funktionen *****************/

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

void copy64 (void)
 {
  BYTE *zeiger_1=maske;
  WORD x,y;
  WORD zoff,zseg;
  BYTE *puffer=NULL;

  asm  cli;                       //Interrupt sperren
  asm  cld;                       //Richtungsflag l”schen

  for (y=2560;y<61440;y=y+5120)
   for (x=0;x<320;x=x+16)
    {
     asm pusha;
     asm push ds;

     puffer=buffer[ (*zeiger_1)&0x0F ];

     zoff= FP_OFF(puffer);
     zseg= FP_SEG(puffer);

        asm {
             mov  ax,x;                 //Offset
             add  ax,y;
             mov  di,ax;                //in DI Register
             mov  ax,0xA000;            //Segment Register mit der
             mov  es,ax;                //Segmentadresse laden
             mov  si,zoff;     //Buffer Anfangsadresse
             mov  ax,zseg;
             mov  ds,ax;
             mov  dx,0x0010;
             mov  cx,dx;

            }
        zeile:;
        asm {
             mov  dx,cx;
             mov  cx,0x0008;            //Z„hler
             rep  movsw;
             add  di,0x0130;
             mov  cx,dx;
             loop zeile;
            }
     zeiger_1++;
    asm pop ds;
    asm popa;

   }
  asm   sti;                       //Interrupt erlauben
 }

void boulder_lauf (void)
 {
  BYTE *zeiger_1 = NULL;
  BYTE *zeiger_2 = NULL;
  BYTE buffer_1  = NULL;
  BYTE i,j,k;

  if (status!=rock_flag)
   {
    for (k=18;k<24;k++)
     {
      for (i=0;i<16;i++)
       {
        zeiger_1 = s_zeiger[k] +i*16 ;
        zeiger_2 = s_zeiger[k] + 15 + i*16;
        for (j=0;j<8;j++)
         {
          buffer_1  = *zeiger_1;
          *zeiger_1 = *zeiger_2;
          *zeiger_2 = buffer_1;
          zeiger_1++;
          zeiger_2--;
         }
       }
     }
    if (rock_flag==0) rock_flag=1;
    else rock_flag=0;

   }
  if (richtung!=0)
   {
    buffer[6]=z_zeiger[wechsel_boulder+18];
    wechsel_boulder++;
    if (wechsel_boulder>5) wechsel_boulder=0;
   }
  else buffer[6]=z_zeiger[13];
 }

void level_laden(BYTE level_nr)
 {
  stop=NULL;
  start=NULL;
  diamanten=NULL;
  blitz  = NULL;
  kop    = NULL;
  flags  = NULL;
  level_ende = NULL;
  clk_1 = NULL;
  clk_2 = NULL;
  clk_4 = NULL;
  clk_18= NULL;
  relx  = NULL;
  rely  = NULL;
  mrun  = NULL;
  richtung        = NULL;
  wechsel_vier    = NULL;
  wechsel_boulder = NULL;
  wechsel_explo   = NULL;
  stat            = NULL;

  wechsel_haus    = NULL;
  anfang_var      = NULL;

  buffer[MASK_HAUSA]=buffer[MASK_STAHL];

  BYTE *zeiger_1 = NULL;
  FILE *dz;
  BYTE xg,yg;
  WORD j;

  if ((dz=fopen(d_name,"r+b"))==NULL) exit(0);

  fseek(dz,(40*22+18)*level_nr,0);
  fread(&level_kopie, sizeof(BYTE), 40*22+16, dz);
  fclose(dz);

  level=level_kopie;                    //Level laden
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
  game_speed = *level; level++;         //Spiel Geschwiendigkeit

  level = level_kopie;
  level = level +16;

  zeiger_1 = level;                         //Boulder Start
  while ( *zeiger_1++ != 10 ) start++;
  zeiger_1 = level;                         //Boulder Stop
  while ( *zeiger_1++ != 11 ) stop++;

  for (j=0;j<240;j++) newmask[j]=48;

  setnewpalette();
 }


BYTE augen=NULL;
BYTE haende=NULL;
/*
void boulder_wait (void)
 {
  if (wechsel_boulder>2)
   {
    buffer[MASK_ROCK] = buffer[augen+SPRITE_ROCK];
    augen++;
    if (augen>3) augen=0, buffer[MASK_ROCK]=buffer[SPRITE_ROCK];
   }
  else
   {
    buffer[MASK_ROCK] = buffer[augen+SPRITE_ROCK+3];
    augen++;
    if (augen>2) augen=0, haende++, buffer[MASK_ROCK]=buffer[SPRITE_ROCK];
    if (haende>3) haende=0, buffer[MASK_ROCK]=buffer[SPRITE_ROCK];
   }
 }*/

BYTE tastatur_code(void)
 {
  BYTE code     = NULL;

  asm in   al,0x60;
  asm mov  code,al;

  return (code);
 }

/* SOUND */

/*void FX(void)
 {
  LAUT_AUS(500,e_zeiger,16000,0x61);
 }*/


