#include <conio.h>
#include <dos.h>
#include <stdio.h>
#include <process.h>

void dunkel (unsigned char verz)
 {
  unsigned char i;
  for (i=63;i>0;i--)            //Verdunklung
   {
    asm {
         mov ah,0x10;
         mov al,0x10;
         mov bx,7;
         mov ch,i;
         mov cl,i;
         mov dh,i;
         int 0x10;
       }
    delay(verz);
   }
 }

void hell (unsigned char verz)
 {
  unsigned char i;
  for (i=0;i<64;i++)            //Verdunklung
   {
    asm {
         mov ah,0x10;
         mov al,0x10;
         mov bx,7;
         mov ch,i;
         mov cl,i;
         mov dh,i;
         int 0x10;
       }
    delay(verz);
   }
 }

/* Soundblaster Initialisierung ******************************************/

void Init_Blaster (void)
 {
  BYTE *cp;
  BYTE FMInit[] = {
      0x23,0x21,  // Tr„ger 1: Multiplikator=1, kontinuierliche Hllkurve
      0x20,0x01,  // Modulator 1: Multiplikator=1, kontinuierliche Hllkurve
      0x43,0,     // Tr„ger 1: keine D„mpfung
      0x40,0,     // Modulator 1: keine D„mpfung
      0x63,0x53,  // T1: Attack und Decay mittel
      0x60,0x53,  // M1: Attack und Decay mittel
      0x83,0x24,  // T1: Sustain und Release mittel
      0x80,0x24,  // M1: Sustain und Release mittel
      0xe0,0,     // Wellenform: Sinus
      0xc0,1,     // Verbindung 1: Addition
      0xFF,0,     // Ende
     };

  printf("[Search]");

  cp = getenv("BLASTER");      // Environmentvariable fr Basisadresse
  if (cp!=NULL)                // Gefunden
    {
     printf(".");
     cp = strchr(cp,'A');      // Suche Annn
     sscanf(cp+1,"%x",&FMAdr); // lese Hexzahl
    }
  else Prog_Exit(" FAILED",INSTALL);

  cp = FMInit;

  while(*cp != 0xFF)
   {
    printf(".");
    Play_Sound(*cp, *(cp+1));
    cp += 2;
   }
  printf("OK");
 }

/* VGA Grafik Initialisierung und Geschwiendigkeitsmessung *****************/

void Init_VGA (void)
 {
  BYTE var1=NULL;

  printf("[Execute]");

  asm {
       mov ah,0x1A;
       mov al,0x00;
       int 0x10;
       mov var1,bl;
      }

    if (var1==8) printf(".");
    else Prog_Exit(" FAILED",INSTALL);
    printf("OK");
 }

/* Processor Check und Geschwiendiegkeitsmessung ***************************/

void Init_Processor(void)
 {
  printf("[Execute].OK");
 }

/* Speicherreservierung ****************************************************/

void Init_Memory(void)
 {
  WORD i=NULL;

  printf("[Alocate]");
  for (i=0;i<49;i++)    //Speicher reservierung fr Sprites
   {
    if (i==48) s_zeiger[48] = (BYTE *) farcalloc(512, sizeof(BYTE));
    else s_zeiger[i] = (BYTE *) farcalloc(256, sizeof(BYTE));
    if (s_zeiger[i] == NULL) Prog_Exit(" farcalloc(s_zeiger) FAILED",INSTALL);
    printf(".");
   }

  e_zeiger = (BYTE *) farcalloc(16*1024,sizeof(BYTE));
  printf(".");
  if (e_zeiger==NULL) Prog_Exit(" farcalloc(e_zeiger) FAILED",INSTALL);
  printf("OK");
 }

/* Sprites in dem Speicher laden *******************************************/

void Load_Sprites(void)
 {
  FILE *dz;
  WORD i=NULL;

  printf("[Alocate]");

  if ((dz=fopen(s_name,"r+b"))==NULL) Prog_Exit(" fopen(s_name) FAILED",INSTALL);
  printf(".");
  for (i=0;i<49;i++)
   {
    if (i==48) fread(s_zeiger[48],sizeof(BYTE),512,dz);
    else fread(s_zeiger[i],sizeof(BYTE),256,dz);
    fgetc(dz); fgetc(dz);
    printf(".");
   }
  if (fclose(dz)==EOF) Prog_Exit(" fclose(s_name) FAILED",INSTALL);
  printf("OK");
 }

/* Levels laden ***********************************************************/

void Load_Levels(void)
 {
  FILE *dz;

  printf("[Execute]");

  if ((dz=fopen(d_name,"r+b"))==NULL) Prog_Exit(" fopen(d_name) FAILED",INSTALL);
  else printf(".");
  if (fclose(dz)==EOF) Prog_Exit(" fclose(d_name) FAILED",INSTALL);
  printf("OK");
 }

/* Demos laden ************************************************************/

void Load_Demos(void)
 {
  FILE *dz;

  printf("[Execute]");

  if ((dz=fopen(f_name,"r+b"))==NULL) Prog_Exit(" fopen(f_name) FAILED",INSTALL);
  else printf(".");
  if (fclose(dz)==EOF) Prog_Exit(" fclose(f_name) FAILED",INSTALL);
  printf("OK");
 }

/* Sound FX laden ********************************************************/

void Load_FX(void)
 {
  FILE *dz;

  printf("[Execute]");

  if((fopen(e_name,"r+b")) == NULL) Prog_Exit(" fopen(e_name) FAILED",INSTALL);
  printf(".");
  fread(e_zeiger,sizeof(BYTE),1024*16,dz);
  printf(".");
  if (fclose(dz)==EOF) Prog_Exit(" fclose(e_name) FAILED",INSTALL);
  printf("OK");
 }

/* Hauptfunktion : INTRO **************************************************/

void Load_Defaults (void)
 {
  inregs.x.ax = 0x0003;                 //Textmodus
  int86 (0x10,&inregs,&outregs);

  clrscr();

  textbackground(RED);  textcolor (BLACK);
  cprintf("                              Boulder Dash Loader v1.0                          ");
  textbackground(BLACK); textcolor (WHITE);

  printf("\nInit_VGA: ");          Init_VGA();
  printf("\nInit_Processor: ");    Init_Processor();
  printf("\nInit_Memory: ");       Init_Memory();
  printf("\nInit_Blaster: ");      Init_Blaster();
  printf("\nLoad_Sprites: ");      Load_Sprites();
  printf("\nLoad_Levels: ");       Load_Levels();
  printf("\nLoad_Demo: ");         Load_Demos();
//  printf("\nLoad_FX: ");           Load_FX();

  delay(2000);

  gotoxy(1,1);
  textbackground(RED);  textcolor (BLACK);
  cprintf("                                 What is the Matrix?                           ");
  textbackground(BLACK); textcolor (WHITE);

  dunkel(25);

  clrscr();
  hell(25);
}
