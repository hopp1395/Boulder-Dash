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
#include <mem.h>

/**************************************************************************/
/* Konstante Variablen                                                    */
/**************************************************************************/

#define ENTER          0x0D
#define ESC            0x1B
#define SPACE          0x20
#define RECHTS         0x4D
#define LINKS          0x4B
#define OBEN           0x48
#define UNTEN          0x50
#define INT_TIMER      0X1C    //Timer Interrupt
#define __CPPARGS ...

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

BYTE orgin_diamant[]="1123003211230032112300321123003211";

BYTE bsp[240];
BYTE level_kopie[40*23];


SWORD bild_xpos=0, bild_ypos=0;
BYTE level_xgr=0, level_ygr=0;

BYTE *buffer[50];
BYTE *lev_nr[3];
BYTE *maske = NULL;
BYTE *level = NULL;
BYTE grundfarbe[4];

BYTE diamanten  = NULL;
BYTE level_ende = NULL;
BYTE score      = NULL;
BYTE leben      = 3;
BYTE level_dnt=0, level_clk=0;

BYTE clk_1 = NULL;
BYTE clk_2 = NULL;
BYTE clk_3 = NULL;
BYTE clk_4 = NULL;
BYTE clk_18= NULL;
WORD start = NULL;
WORD stop  = NULL;
char relx  = NULL;
char rely  = NULL;

/************ Funktions spezifische Wariablen *****************************/

signed char richtung        = NULL;
signed char richtung_alt    = NULL;

BYTE status          = NULL;
BYTE wechsel_boulder1= NULL;
BYTE wechsel_vier    = NULL;
BYTE wechsel_boulder = NULL;
BYTE wechsel_diamant = NULL;
BYTE wechsel_explo   = NULL;

BYTE wechsel_haus    = NULL;
BYTE anfang_var      = NULL;

/**************************************************************************/
/* Zeiger                                                                 */
/**************************************************************************/

void interrupt (*alte_timer_isr)(__CPPARGS);     //Alte Timer ISR

/**************************************************************************/
/* Funktionen (Vordefinition)                                             */
/**************************************************************************/

void interrupt timer_interrupt_service_routine (__CPPARGS);

void BildschirmAufbau(void);
void copy64 (WORD x, WORD y, BYTE *buffer);
BYTE *BildschirmMaske (BYTE *level, SWORD bild_xpos, SWORD bild_ypos);
void diamant_convert(BYTE pos);
void convert (BYTE *zeiger);
void boulder_wait   (void);
void boulder_lauf (void);
void sprite_wechsel (void);
void sprite_diamant (void);
void sprite_haus (void);
void sprite_explo (void);
void regel (void);
void anfang (void);
void ende (void);
void explosion_diamant(void);

#include "level.cpp"

/**************************************************************************/
/* Hauptprogramm                                                          */
/**************************************************************************/

BYTE tastatur_code(void)
 {
  BYTE code = NULL;
  l1:;
  asm {
       in       al,0x64;
       and      al,0x01;
       cmp      al,0x01;
       jnz      l1;
       in       al,0x60;
       mov      code,al;
      }
  return (code);
 }

void main (void)
 {
  BYTE c=0;                               //Buffer fÅr Tastatureingabe
  BYTE *zeiger_1 = NULL;
  BYTE level_nr  = 2;
  WORD i=NULL;

  outp  (0x21,0x02);     //IRQ 1 sperren

  outp (0x60,0xED);
  warten:;
  asm {
        in      al,0x64;
        test    al,0x02;
        jnz     warten;
       }
  outp (0x60,0x00);

  alte_timer_isr = getvect(INT_TIMER);             //Alten Interrupt sichern
  setvect(INT_TIMER, timer_interrupt_service_routine);  //neuen Installieren

  inregs.x.ax = 0x0013;                //Grafikmodus 320x200x256
  int86 (0x10,&inregs,&outregs);        //Bildschirspeicher ab A0000 (64k)

  sprites_laden();                      //Sprite Zeiger initialisierung

  level=lev_nr[level_nr];               //Level laden
  level_xgr = *level; level++;          //Level X-Grî·e
  level_ygr = *level; level++;          //Level Y-Grî·e
  level_dnt = *level; level++;          //Minimale Diamanten Anzahl
  level_clk = *level; level++;          //Level Zeit
  grundfarbe[0] = *level; level++;
  grundfarbe[1] = *level; level++;
  grundfarbe[2] = *level; level++;
  grundfarbe[3] = *level; level++;
  bild_xpos = *level; level++;
  bild_ypos = *level; level++;

  for (i=0;i<level_xgr*level_ygr;i++) *level++=*level-0x30;
  level = lev_nr[level_nr] + 10;
  for (i=0;i<42;i++) convert(buffer[i]);

  memcpy (level_kopie,level,level_xgr*level_ygr);    //Level Kopie erzeugen

  level = level_kopie;                              //Zeiger zuweisen

  zeiger_1 = level;                         //Boulder Start
  while ( *zeiger_1++ != 10 ) start++;
  zeiger_1 = level;                         //Boulder Stop
  while ( *zeiger_1++ != 11 ) stop++;

  while (c!=0x01)
   {
    c=tastatur_code();
    if (c==0x01) break;

    if (c==0xE0)
     {
      c=tastatur_code();

      switch(c)
       {
        case 0x4D : richtung=1; status=0; break;
        case 0x4B : richtung= (-1); status=1; break;
        case 0x50 : richtung=level_xgr; break;
        case 0x48 : richtung=(-1)*level_xgr; break;
        default   : richtung=0; break;
       }
     }
    gotoxy(1,1); printf("TIME:%03i MAN:%02i LEVEL:A1  %02i-%02i   %06i",level_clk,3,diamanten,level_dnt,score);
   }
  setvect(INT_TIMER, alte_timer_isr);        //Alten Interrupt wiederherstellen

  outp (0x21,0x02);                     //IRQ 1 sperren
  outp (0x60,0xF6);                     //Tastaturbefehl Standartwerte*/
  outp (0x21,0x00);                     //IRQ 1 freigeben

  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);
 }

/****************************** Funktionen ************************/

void interrupt timer_interrupt_service_routine (__CPPARGS)
 {
  alte_timer_isr();     //Alte ISR ausfÅhren

  if (clk_1++>1)   clk_1=0;
  if (clk_2++>2)   clk_2=0;
  if (clk_3++>3)   clk_3=0;
  if (clk_4++>4)   clk_4=0;
  if (clk_18++>18) clk_18=0;

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
  convert(buffer[3]);
  boulder_lauf();            //Boulder Animaton
  sprite_explo();

  if (clk_18==0) level_clk--;
  if (level_clk==0) level_ende=0xFF;

  if (level_ende!=0xFF)
   {
    if (clk_1==0) regel();
    if (anfang_var<166) anfang();
    if (diamanten>=level_dnt) ende();
   }
  maske = BildschirmMaske(level,bild_xpos,bild_ypos);   //Bildschirmmaske
  BildschirmAufbau();                                   //Bildschirm aufbauen
 }

void anfang (void)
 {
  if (anfang_var<160)
   {
    if (clk_4<3) buffer[10]=buffer[14];
    else buffer[10]=buffer[5];
   }
  if (anfang_var==160) *(level+start)=12, wechsel_explo=0;
  if (anfang_var==165) *(level+start)=6;
  anfang_var++;
 }

void ende (void)
 {
  if (clk_4<3) buffer[11]=buffer[14];
  else buffer[11]=buffer[5];
 }

void sprite_explo(void)
 {
  if (wechsel_explo<6)
   {
    if (wechsel_explo<3) buffer[12]=buffer[39+wechsel_explo];
    else buffer[12]=buffer[44-wechsel_explo];
    wechsel_explo++;
   }
 }

void sprite_haus(void)
 {
  if (clk_2<1) buffer[10]=buffer[14];
  else buffer[10]=buffer[5];
 }

void sprite_wechsel(void)
 {
  buffer[7]=buffer[wechsel_vier+26];
  buffer[8]=buffer[wechsel_vier+30];
  buffer[9]=buffer[wechsel_vier+34];
  wechsel_vier++;
  if (wechsel_vier>3) wechsel_vier=0;
 }

BYTE *BildschirmMaske (BYTE *level, SWORD bild_xpos, SWORD bild_ypos)
 {
  int i,j;
  BYTE xgr=level_xgr;
  BYTE ygr=level_ygr;
  BYTE *zeiger1 = level;
  BYTE *zeiger2 = bsp;
  BYTE *zeiger3 = bsp;

  zeiger1 = zeiger1 + ((bild_ypos)*xgr) + bild_xpos;

  for (i=0;i<12;i++)
   {
    memcpy (zeiger2,zeiger1,20);
    zeiger1 = zeiger1 + xgr;
    zeiger2 = zeiger2 + 20;
   }
  return(zeiger3);
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
  BYTE j=0,w=0;
  BYTE *zeiger_2 = level;

  for (j=0;j<level_ygr;j++)
   {
    for (i=0;i<level_xgr;i++)
     {
  /************* Explosion -> Diamant ******************************/
  /************* Explosion -> Leer *********************************/
      if (((*zeiger_2&0x9F)==12) && (wechsel_explo>2))
       {
        if ((*zeiger_2&0xC0) == 0x40) *zeiger_2 = 0x83;
        if ((*zeiger_2&0xC0) == 0x00) *zeiger_2 = 0x80;
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
        for (w=0;w<4;w++)
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
       }
  /************* Geist *********************************************/
      if ( ((*zeiger_2&0x9F)==8) )
       {
        if ( (*(zeiger_2-level_xgr)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2+level_xgr)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2-1)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2+1)&0xFE)==6 ) explosion(0x8C,zeiger_2);
        else if ( (*(zeiger_2-level_xgr)&0x4C)==0x40  ) explosion(0x8C,zeiger_2);
        else
        for (w=0;w<4;w++)
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
       }
  /************* Stein, Diamant ****************************************/
      if (((*zeiger_2&0xBF) == 3) || ((*zeiger_2&0xBF) == 2))  //Stein oder Diamant?
       {
        switch ( *(zeiger_2+level_xgr)&0x1F )         //Was ist drunter?
         {
          default: *zeiger_2 = *zeiger_2&0x1F;
          case 9 :
          case 8 : break;
          case 0 : *(zeiger_2+level_xgr) = *zeiger_2|0xC0;  //nichts?
                     *zeiger_2 = 0;
                     break;
          case 2 :                                     //Stein?
          case 3 :                                     //Diamant? oder Mauer?
          case 4 : if ( ( *(zeiger_2+1)==0) && (*(zeiger_2+1+level_xgr)==0))
                      {
                       *(zeiger_2+1) = *zeiger_2|0xC0;
                       *zeiger_2 = 0;
                      }
                     else
                     if ((*(zeiger_2-1)== 0) && (*(zeiger_2-1+level_xgr)==0))
                       {
                       *(zeiger_2-1) = *zeiger_2|0xC0;
                       *zeiger_2 = 0;
                      }
                     else *zeiger_2 = *zeiger_2|0x80;
                     break;
         }
       }

  /************* Boulder ****************************************/
    if ((*zeiger_2&0x9F) == 6)
       {
        if ((bild_xpos+16<i)&&(bild_xpos<level_xgr-20)) relx=7;
        if ((bild_xpos+2==i)&&(bild_xpos>0)) relx=-7;
        if ((bild_ypos+9<j)&&(bild_ypos<level_ygr-12)) rely=5;
        if ((bild_ypos+2==j)&&(bild_ypos>0)) rely=-5;

//        if ( (*(zeiger_2-level_xgr)&0xC2)==0xC2) explosion(0x8C,zeiger_2);
//        if ( (*(zeiger_2-level_xgr)&0x4C)==0x40  ) explosion(0x8C,zeiger_2);
        else
        switch ( *(zeiger_2+richtung)&0x1F)
         {
          default: break;
          case 11: if (diamanten<level_dnt) break;
                   level_ende=0xFF;
                   anfang_var=0;
          case 3 : diamanten++;
          case 0 :
          case 1 : *(zeiger_2+richtung) = 0x86;
                   *zeiger_2=0;
                   break;
          case 2 : if  ((*(zeiger_2+(richtung*2))==0)&&(clk_4==0))
                      {
                       if ((richtung==1)||(richtung==-1))
                        {
                         *(zeiger_2+richtung) = 0x86;
                         *(zeiger_2+(2*richtung))= 0x82;
                         *zeiger_2=0;
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

 }


/******************* Fertige Funktionen *****************/

void sprite_diamant (void)
 {
  int dx,dy;
  BYTE *zbuffer1 = buffer[38];
  BYTE *zbuffer2 = buffer[3];
  BYTE *zbuffer3 = orgin_diamant;

  zbuffer3 = zbuffer3 + wechsel_diamant;

  if (wechsel_diamant>14) wechsel_diamant=0;

  for (dy=0;dy<16;dy++)
   {
    for (dx=0;dx<16;dx++)
     {
      if ( (*zbuffer1) > 64 ) *zbuffer2 = *zbuffer3;
      zbuffer1++;
      zbuffer2++;
     }
    zbuffer3++;
   }
  wechsel_diamant++;
 }


void BildschirmAufbau(void)
 {
  int x,y;
  for (y=0;y<192;y=y+16)
   for (x=0;x<320;x=x+16)
    {
     copy64 (x,y,(buffer[(*maske)&0x1F]));
     maske++;
    }
 }

void convert (BYTE *zeiger)
 {
 int i;
  for (i=0;i<256;i++)
   {
    if (*zeiger=='0') *zeiger = grundfarbe[0];
    if (*zeiger=='1') *zeiger = grundfarbe[1];
    if (*zeiger=='2') *zeiger = grundfarbe[2];
    if (*zeiger=='3') *zeiger = grundfarbe[3];
    zeiger++;
   }
 }

void copy64 (WORD x, WORD y, BYTE *buffer)
 {
        asm  cli;                       //Interrupt sperren
        asm  cld;                       //Richtungsflag lîschen
        asm {
             mov  ax,0x0140;            //Adresse ermitteln
             mov  bx,y;                 //Zeilenadresse
             mul  bx;                   //ermitteln
             add  ax,x;                 //Zeilenadresse + Spalte = Adresse
             mov  di,ax;                //in DI Register
             mov  ax,0xA0A0;            //Segment Register mit der
             mov  es,ax;                //Segmentadresse laden
             mov  si,buffer;            //Buffer Anfangsadresse
             mov  dx,0x0010;
             mov  cx,dx;
            }
zeile:;
        asm {
             mov  dx,cx;
             mov  cx,0x0008;            //ZÑhler
             rep  movsw;
             add  di,0x0130;
             mov  cx,dx;
             loop zeile;
            }
       asm   sti;                       //Interrupt erlauben
 }



void boulder_lauf (void)
 {
  BYTE *zeiger_1 = NULL;
  BYTE *zeiger_2 = NULL;
  BYTE buffer_1  = NULL;
  BYTE i,j,k;

  if (status!=richtung_alt)
   {
    for (k=15;k<21;k++)
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
   }
  if (richtung!=0)
   {
    buffer[6]=buffer[wechsel_boulder1+15];
    wechsel_boulder1++;
    if (wechsel_boulder1>5) wechsel_boulder1=0;
   }
  else buffer[6]=buffer[21];
  richtung_alt=status;
 }
 /*
void boulder_wait (void)
 {
  if (wechsel_boulder>2)
   {
    buffer[6] = buffer[augen+15];
    augen++;
    if (augen>3) augen=0, wait=0, buffer[6]=buffer[15];
   }
  else
   {
    buffer[6] = buffer[augen+18];
    augen++;
    if (augen>2) augen=0, haende++, buffer[6]=buffer[15];
    if (haende>3) haende=0, wait=0, buffer[6]=buffer[15];
   }
 } */
