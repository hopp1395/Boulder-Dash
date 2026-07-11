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
#include <dos.h>
#include <string.h>
#include <time.h>

#include "intro.cpp"

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
#define SPRITE_HAUS    14
#define SPRITE_SLAUF   15
#define SPRITE_ROCK    16
#define SPRITE_LAVA    27
#define SPRITE_GEIST   31
#define SPRITE_MOTYL   35
#define SPRITE_EXPLO   40
#define SPRITE_MAUER   43
#define SPRITE_LEER    47
#define SPRITE_DIAMANT 48

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
BYTE level_xgr=0, level_ygr=0;

BYTE *buffer[49];
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

BYTE orgin_diamant[]="1123003211230032112300321123003211";
BYTE *menutext="                                        ";
WORD menuzeit=0;

signed char richtung        = NULL;
signed char richtung_alt    = NULL;
BYTE *d_name="level.bin";

BYTE status          = NULL;
BYTE wechsel_boulder1= NULL;
BYTE wechsel_vier    = NULL;
BYTE wechsel_boulder = NULL;
BYTE wechsel_diamant = NULL;
BYTE wechsel_explo   = NULL;

BYTE wechsel_haus    = NULL;
BYTE anfang_var      = NULL;
BYTE rock_flag       = NULL;

/**************************************************************************/
/* Zeiger                                                                 */
/**************************************************************************/

void interrupt (*alte_timer_isr)(__CPPARGS);     //Alte Timer ISR

/**************************************************************************/
/* Funktionen (Vordefinition)                                             */
/**************************************************************************/

void interrupt timer_interrupt_service_routine (__CPPARGS);
void interrupt menu_interrupt_service_routine (__CPPARGS);

void level_laden (BYTE level_nr);
void copy64 (void);
void BildschirmMaske (SWORD bild_xpos, SWORD bild_ypos);
void diamant_convert(BYTE pos);
void boulder_wait   (void);
void boulder_lauf (void);
void go (void);
void sprite_wechsel (void);
void sprite_diamant (void);
void sprite_haus (void);
void sprite_explo (void);
void regel (void);
void anfang (void);
void ende (void);
void explosion_diamant(void);
BYTE tastatur_code(void);
void hauptmenu(void);
void game_start(void);
void setvgapalette (WORD nr, BYTE g, BYTE r, BYTE b);
void setnewpalette(void);
void anfangs_maske(void);
void sprite_stahl (void);

#include "level.cpp"

/**************************************************************************/
/* Hauptprogramm                                                          */
/**************************************************************************/

void main (char argc, char *argv[2])
 {
  BYTE i=0;

  if (argc==2) d_name=argv[1];

//  intro();

  inregs.x.ax = 0x0013;                 //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  textcolor(0);
  textbackground(1);


  for (i=0;i<34;i++) orgin_diamant[i]=orgin_diamant[i]-0x30;

  sprites_laden();

  hauptmenu();

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
  gotoxy(10,6); printf("³ Boulder Dash v1.0 ³");
  gotoxy(10,7); printf("ÀÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÙ");
  gotoxy(1,16); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
  gotoxy(1,17); printf("      Copyright 1999 by Jan Hoppe       ");
  gotoxy(1,18); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
  gotoxy(1,19); printf(" F1 - Spiel starten  F2 - Einleitung    ");
  gotoxy(1,21); printf(" F3 - Hilfe          F4 - Ende          ");
  gotoxy(1,22); printf("ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ");
 }

void interrupt menu_interrupt_service_routine (__CPPARGS)
 {
  WORD i=NULL;

  BYTE *z1="                                        Boulde Dash v1.0                        "
           "Copyright by Jan Hoppe 1999             "
           "Drcken Sie bitte die F1 Taste um das Sp"
           "iel zu starten                          "
           "                                        ";

  i=strlen(z1);

  if (clk_18++>1)
   {
    clk_18=0;
    if (menuzeit>(i-40)) menuzeit=0;
    memcpy(menutext,z1+menuzeit,40);
    menuzeit++;
   }
 }

void hauptmenu(void)
 {
  BYTE c=1;
  SBYTE cavenr=0;
  SBYTE levelnr=1;

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
        case LINKS : cavenr=cavenr+5;
                     if (cavenr>15) cavenr=15;
                     break;
        case RECHTS: cavenr=cavenr-5;
                     if (cavenr<0)  cavenr=0;
                     break;
        case F1    : setvect(INT_TIMER, alte_timer_isr);
                     level_next=0;
                     level_ende=0xFF;
                     while (level_ende==0xFF)
                      {
                       level_laden(cavenr+level_next);
                       game_start();
                       if (level_next==21) level_next=0;
                       if (leben<1) break;
                       if (stat!=0) level_ende=0xFF;
                      }
                     menuausgabe();
                     alte_timer_isr = getvect(INT_TIMER);
                     setvect(INT_TIMER, menu_interrupt_service_routine);
                     break;
       }
     }
   }
  setvect(INT_TIMER, alte_timer_isr);        //Alten Interrupt wiederherstellen
 }

void game_start(void)
 {
    WORD i=0;
    clock_t start, end;
    start = clock();

    for (i=0;i<1000;i++) go();

    end = clock();
    clrscr();
    printf("%f Bps\n", 1000 / ((end - start) / CLK_TCK));
    getch();
 }


/****************************** Funktionen ************************/

void go(void)
 {
  if (stoss++>1) stoss=0;
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

    sprite_wechsel();          //Lava, Geist, Schmeterling Animation
    sprite_diamant();          //Diamant Animation
    boulder_lauf();            //Boulder Animaton
    sprite_explo();

    if (clk_18==0)
     {
//      level_clk--;
//      if (mrun==1) mtime--;
     }

    if (mtime==0) mrun=0;
    if (level_clk==0) level_ende=0xFF;


    if (level_ende!=0xFF)
     {
      if ((clk_1==0)&&(anfang_var>90)) regel();
      if (anfang_var<166) anfang();
      if (diamanten>=level_dnt) ende();
     }
    BildschirmMaske(bild_xpos,bild_ypos);   //Bildschirmmaske
    maske=bsp;
    if (anfang_var<160) anfangs_maske();
    copy64();                          //Bildschirm aufbauen
 }

void sprite_stahl (void)
 {
  WORD i=NULL;
  BYTE sb[16];
  memcpy(sb,buffer[SPRITE_SLAUF],16);
  memmove(buffer[SPRITE_SLAUF],buffer[SPRITE_SLAUF]+16,240);
  memcpy (buffer[SPRITE_SLAUF]+240,sb,16);
 }

void anfangs_maske(void)
 {
  sprite_stahl();
  WORD i=NULL, j=NULL, z=NULL;
  BYTE *mz=maske;
  if (anfang_var<10) for (i=0;i<240;i++) newmask[i]=SPRITE_SLAUF;
  else
   {
    for (z=0;z<4;z++)
     {
      while (newmask[j]==0)
       {
        j=(rand()%240)+1;
        i++;
        if (i==240) break;
       }
      newmask[j]=0;
      j=0;
     }
   }
  for (i=0;i<240;i++)
   if (newmask[i]==SPRITE_SLAUF) *(mz+i) = SPRITE_SLAUF;
 }

void sprite_diamant (void)
 {
  int dx,dy;
  BYTE *zbuffer1 = buffer[SPRITE_DIAMANT];
  BYTE *zbuffer2 = buffer[MASK_DIAMANT];
  BYTE *zbuffer3 = orgin_diamant;

  zbuffer3 = zbuffer3 + wechsel_diamant;

  if (wechsel_diamant>15) wechsel_diamant=0;

  for (dy=0;dy<16;dy++)
   {
    for (dx=0;dx<16;dx++)
     {
      if ( (*zbuffer1) > 4 ) *zbuffer2 = *zbuffer3;
      zbuffer1++;
      zbuffer2++;
     }
    zbuffer3++;
   }
  wechsel_diamant++;
 }

void anfang (void)
 {
  if (anfang_var<160)
   {
    if (clk_4<3) buffer[MASK_HAUSE]=buffer[SPRITE_HAUS];
    else buffer[MASK_HAUSE]=buffer[MASK_STAHL];
   }
  if (anfang_var==160) *(level+start)=12, wechsel_explo=0;
  if (anfang_var==165) *(level+start)=6;
  anfang_var++;
 }

void ende (void)
 {
  if (blitz==0) setvgapalette(0,63,63,63), blitz=1;
  else setvgapalette (0,8,8,8);
  if (clk_4<3) buffer[MASK_HAUSA]=buffer[SPRITE_HAUS];
  else buffer[MASK_HAUSA]=buffer[MASK_STAHL];
 }

void sprite_explo(void)
 {
  if (wechsel_explo<6)
   {
    if (wechsel_explo<3) buffer[MASK_EXPLO]=buffer[SPRITE_EXPLO+wechsel_explo];
    else buffer[MASK_EXPLO]=buffer[SPRITE_EXPLO+5-wechsel_explo];
    wechsel_explo++;
   }
 }

void sprite_haus(void)
 {
  if (clk_2<1) buffer[MASK_HAUSA]=buffer[SPRITE_HAUS];
  else buffer[MASK_HAUSA]=buffer[MASK_STAHL];
 }

void sprite_wechsel(void)
 {
  buffer[MASK_LAVA]=buffer[wechsel_vier+SPRITE_LAVA];
  buffer[MASK_GEIST]=buffer[wechsel_vier+SPRITE_GEIST];
  buffer[MASK_MOTYL]=buffer[wechsel_vier+SPRITE_MOTYL];
  if (mrun==1) buffer[MASK_MAUERU]=buffer[wechsel_vier+SPRITE_MAUER];
  else buffer[MASK_MAUERU]=buffer[MASK_MAUER];
  wechsel_vier++;
  if (wechsel_vier>3) wechsel_vier=0;
 }

void BildschirmMaske (SWORD bild_xpos, SWORD bild_ypos)
 {
  int i,j;
  BYTE *zeiger1 = level;
  BYTE *zeiger2 = bsp;

  zeiger1 = zeiger1 + ((bild_ypos)*level_xgr) + bild_xpos;

  for (i=0;i<12;i++)
   {
    memcpy (zeiger2,zeiger1,20);
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
  wechsel_explo=0;
 }

void regel (void)
 {
  WORD i=0;
  BYTE lava_var=0;
  BYTE lava_nr=(rand()%96)+1;
  BYTE j=0,w=0;
  BYTE *zeiger_2 = level;
  BYTE lf=3;

  if ((anfang_var>164)&&(stat==0)) stat=1, leben--;

  for (j=0;j<level_ygr;j++)
   {
    for (i=0;i<level_xgr;i++)
     {
  /************* Explosion -> Diamant ******************************/
  /************* Explosion -> Leer *********************************/
      if (((*zeiger_2&0x9F)==12) && (wechsel_explo>5))
       {
        if ((*zeiger_2&0xC0) == 0x40) *zeiger_2 = 0x83;
        if ((*zeiger_2&0xC0) == 0x00) *zeiger_2 = 0x80;
       }
  /************* Lava **********************************************/
      if ((*zeiger_2&0x9F)==0x07)
       {
        lava_var++;
        if (lava_var==lava_nr)
         {
          if ( (*(zeiger_2-level_xgr)&0xFE)==0 ) *(zeiger_2-level_xgr)=0x87;
          else if ( (*(zeiger_2+level_xgr)&0xFE)==0 ) *(zeiger_2+level_xgr)=0x87;
          else if ( (*(zeiger_2-1)&0xFE)==0 ) *(zeiger_2-1)=0x87;
          else if ( (*(zeiger_2+1)&0xFE)==0 ) *(zeiger_2+1)=0x87;
         }
       }

  /************* Schmeterling **************************************/
      if ( ((*zeiger_2&0x9F)==9) )
       {
        // Lava | Rockford //
        if ( (*(zeiger_2-level_xgr)&0xFE)==6 ) explosion(0xCC,zeiger_2);
        else if ( (*(zeiger_2+level_xgr)&0xFE)==6 ) explosion(0xCC,zeiger_2);
        else if ( (*(zeiger_2-1)&0xFE)==6 ) explosion(0xCC,zeiger_2);
        else if ( (*(zeiger_2+1)&0xFE)==6 ) explosion(0xCC,zeiger_2);
        else if ( (*(zeiger_2-level_xgr)&0x4C)==0x40) explosion(0xCC,zeiger_2);
        else
        for (w=0;w<3;w++)
         {
          switch (*zeiger_2&0x60)
           {
            case 0x00 : if (*(zeiger_2-level_xgr) ==0)
                          *(zeiger_2-level_xgr) = 0xA9, *zeiger_2 = 0, w=4;
                        else *zeiger_2 = 0x69;
                        break;
            case 0x20 : if ( *(zeiger_2+1) == 0)
                          *(zeiger_2+1) = 0xC9, *zeiger_2 = 0, w=4;
                        else *zeiger_2 =  0x09;
                        break;
            case 0x40 : if ( *(zeiger_2+level_xgr) == 0)
                          *(zeiger_2+level_xgr) =  0xE9, *zeiger_2 = 0, w=4;
                        else *zeiger_2 = 0x29;
                        break;
            case 0x60 : if ( *(zeiger_2-1) == 0)
                          *(zeiger_2-1) =  0x89, *zeiger_2 = 0, w=4;
                        else *zeiger_2 =  0x49;
            default   : break;
           }
         }
        *zeiger_2=*zeiger_2|0x80;
       }
  /************* Geist *********************************************/
      if ( ((*zeiger_2&0x9F)==8) )
       {
        if ( (*(zeiger_2-level_xgr)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2+level_xgr)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2-1)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2+1)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2-level_xgr)&0x42)==0x42  ) explosion(0x8C,zeiger_2);
        else
        for (w=0;w<3;w++)
         {
          switch (*zeiger_2&0x60)
           {
            case 0x00 : if (*(zeiger_2+level_xgr) ==0)
                          *(zeiger_2+level_xgr) = 0xE8, *zeiger_2 = 0, w=4;
                        else *zeiger_2 = 0x28;
                        break;
            case 0x20 : if ( *(zeiger_2-1) == 0)
                          *(zeiger_2-1) = 0x88, *zeiger_2 = 0, w=4;
                        else *zeiger_2 =  0x48;
                        break;
            case 0x40 : if ( *(zeiger_2-level_xgr) == 0)
                          *(zeiger_2-level_xgr) =  0xA8, *zeiger_2 = 0, w=4;
                        else *zeiger_2 = 0x68;
                        break;
            case 0x60 : if ( *(zeiger_2+1) == 0)
                          *(zeiger_2+1) =  0xC8, *zeiger_2 = 0, w=4;
                        else *zeiger_2 =  0x08;
            default   : break;
           }
         }
        *zeiger_2=*zeiger_2|0x80;
       }
  /************* Stein, Diamant ****************************************/
      if (((*zeiger_2&0x9F) == 3) || ((*zeiger_2&0x9F) == 2))  //Stein oder Diamant?
       {
        switch ( *(zeiger_2+level_xgr)&0x0F )         //Was ist drunter?
         {
          default: *zeiger_2 = *zeiger_2&0x1F;
          case 9 :
          case 8 : break;
          case 0 : *(zeiger_2+level_xgr) = *zeiger_2|0xC0;  //nichts?
                     *zeiger_2 = 0x80;
                     break;
          case 2 :                                     //Stein?
          case 3 :                                     //Diamant? oder Mauer?
          case 4 : if ((*(zeiger_2-1)== 0) && (*(zeiger_2-1+level_xgr)==0))
                    {
                     *(zeiger_2-1) = *zeiger_2|0x80;
                     *zeiger_2 = 0x80;
                     *(zeiger_2-1+level_xgr)= 0x80;
                    }
                   else
                   if ( ( *(zeiger_2+1)==0) && (*(zeiger_2+1+level_xgr)==0))
                    {
                     *(zeiger_2+1) = *zeiger_2|0x80;
                     *zeiger_2 = 0x80;
                     *(zeiger_2+1+level_xgr)= 0x80;
                    }

                     else *zeiger_2 = *zeiger_2&0x1F;
                     break;
          case 13 : if (mtime>0)
                     {
                      if ((*zeiger_2&0x40)==0x40)
                       {
                        mrun=1;
                        if (*(zeiger_2+2*level_xgr)==0)
                         *(zeiger_2+2*level_xgr) = (*zeiger_2 - 1)|0xC2;
                        *zeiger_2=0x80;
                       }

                     }
                    else if ((*zeiger_2&0x40)==0x40) *zeiger_2=0;
                    break;
          case 6  : if (((*(zeiger_2+level_xgr)&0x06)==0x06)&&
                      ((*zeiger_2&0x4C)==0x40)) explosion(0x8C,zeiger_2+level_xgr);
                    break;
           }
         }

  /************* Boulder ****************************************/
    if ((*zeiger_2&0x9F) == 6)
       {
        stat=0;
        leben++;
        if ((bild_xpos+17<i)&&(bild_xpos<level_xgr-20)) relx=7;
        if ((bild_xpos+1==i)&&(bild_xpos>0)) relx=-7;
        if ((bild_ypos+9<j)&&(bild_ypos<level_ygr-12)) rely=5;
        if ((bild_ypos+1==j)&&(bild_ypos>0)) rely=-5;
        else
        switch ( *(zeiger_2+richtung)&0x9F)
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
          case 1 : *(zeiger_2+richtung) = 0x86 ^ kop;
                   *zeiger_2=0x80 ^ kop;
                   break;
          case 2 : if  ((*(zeiger_2+(richtung*2))==0)&&(clk_4==0))
                      {
                       if ((richtung==1)||(richtung==-1))
                        {
                         *(zeiger_2+richtung) = 0x86 ^ kop;
                         *(zeiger_2+(2*richtung))= 0x82;
                         *zeiger_2=0x80 ^ kop;
                       }
                      }
                     break;
         }
       }
      zeiger_2++;
     }
   }

  zeiger_2=level;

  for (i=0;i<level_xgr*level_ygr;i++)
   {
    *zeiger_2=*zeiger_2&0x7F;
    zeiger_2++;
   }
  zeiger_2=level;

  for (i=0;i<level_xgr*level_ygr;i++)
   {
    if (*zeiger_2==7)
     {
      if ( ((*(zeiger_2-level_xgr)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(zeiger_2+level_xgr)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(zeiger_2-1)&0xFE)|1)==1 ) lf=2;
      else if ( ((*(zeiger_2+1)&0xFE)|1)==1 ) lf=2;
     }
    zeiger_2++;
   }

 zeiger_2=level;

  if ((lava_var>95)||(lf==3))
   for (i=0;i<level_xgr*level_ygr;i++)
    {
     if (*zeiger_2==7) *zeiger_2=lf;
     zeiger_2++;
    }

  zeiger_2=level;

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
  BYTE *puffer=NULL;
  WORD x,y;

  asm  cli;                       //Interrupt sperren
  asm  cld;                       //Richtungsflag l”schen

  for (y=2560;y<61440;y=y+5120)
   for (x=0;x<320;x=x+16)
    {
     puffer=buffer[ (*zeiger_1)&0x1F ];

        asm {
             mov  ax,x;                 //Offset
             add  ax,y;
             mov  di,ax;                //in DI Register
             mov  ax,0xA000;            //Segment Register mit der
             mov  es,ax;                //Segmentadresse laden
             mov  si,puffer;            //Buffer Anfangsadresse
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
    for (k=SPRITE_ROCK;k<SPRITE_ROCK+6;k++)
     {
      for (i=0;i<16;i++)
       {
        zeiger_1 = buffer[k] +i*16 ;
        zeiger_2 = buffer[k] + 15 + i*16;
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
    buffer[6]=buffer[wechsel_boulder1+SPRITE_ROCK];
    wechsel_boulder1++;
    if (wechsel_boulder1>5) wechsel_boulder1=0;
   }
  else buffer[6]=buffer[SPRITE_ROCK+6];
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
  wechsel_boulder1= NULL;
  wechsel_vier    = NULL;
  wechsel_boulder = NULL;
  wechsel_diamant = NULL;
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
 }

BYTE tastatur_code(void)
 {
  BYTE code     = NULL;

  asm in   al,0x60;
  asm mov  code,al;

  return (code);
 }

