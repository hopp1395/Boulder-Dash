#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <conio.h>
#include <dos.h>
#include <io.h>
#include <fcntl.h>

#define BYTE unsigned char
#define WORD unsigned int
#define LOBYTE(w) ((BYTE) ((w) & 0xFF))
#define HIBYTE(w) ((BYTE) (((w) >> 8) & 0xFF))
#define DSP_WRITESTATUS 0x0C
#define DSP_WRITECMDDATA 0x0C
#define DSP_READSTATUS 0x0E
#define DSP_READDATA 0x0A
#define DSP_RESET 0x06
#define MIX_REGISTERPORT 0x04
#define MIX_DATAPORT 0x05
#define MIX4_IRQSOURCE 0x82
#define MIX4_IRQ8DMA 0x01
#define MIX4_IRQ16DMA 0x02
#define MIX4_IRQMPU 0x04
/* IRQ-Controller Portadressen */
#define MASTER_8259 0x20
#define SLAVE_8259 0xA0
/* Unspezifiz. End-of-Interrupt-Komm.*/
#define EOI 0x20

int DspPort; /* Basisport DSP */
int MixPort; /* Basisport Mixer */
int MpuPort; /* Basisport MP-Einheit */
int DspDmaB; /* 8-Bit DMA Kanal */
int DspDmaW; /* 16-Bit DMA Kanal */

int DspIrq; /* Interrupt-Leitung */
int BufCnt; /* Prototypen: */
void start_dma_dsp(void);
void timeprint(long fpos);
void stop_playing(void);
void dsp_Write(WORD Val);
void dsp_Read(BYTE * Val);
WORD dsp_Reset(void);
void dma_SetMask(int Chan);
void dma_ClrMask(int Chan);
void dma_ClrFlipFlop(int Chan);
void dma_SetChannel(int Chan, void far
*mem, WORD Size, BYTE Mode);
void irq_onoff(int IRQ, int onoff);
void mix_Write(WORD Reg, WORD Data);
void interrupt far dsp_IrqHandler();

#define DMABUFSIZE 28224 /* 80 msec, 3528 Samples pro Kanal und Halbbuffer */

BYTE dmabuf[2*DMABUFSIZE];
void (interrupt far *OldInt)() = NULL;
WORD irqs = 0, cmd, dspmode=0;
BYTE *pBuff2;
BYTE far *fpBuff;
BYTE *pBuffer0, *pBuffer1, *pakt;
int handle;

void (interrupt far *irq_SetHandler(int iIRQ,void (interrupt far *Handler)()) ) ()
 {
  void (interrupt far *OldHandler)();
  int  iVect;
  /*IRQ 0 - 7  = Vektoren 0x08 - 0x0F*/
  /*IRQ 8 - 15 = Vektoren 0x70 - 0x77*/

  iVect = (iIRQ <= 7) ? (8 + iIRQ) : (0x70 + (iIRQ & 0x0007) );
   irq_onoff(iIRQ,0);/* HW-INT sperren*/
   _disable(); /* alle INTS sperren */
   OldHandler = _dos_getvect(iVect);
   _dos_setvect(iVect, Handler);
   _enable(); /* Interrupts freigeben */
   irq_onoff(iIRQ,1);/*HW-INT freig.*/
   return OldHandler;
 }

int main(int argc, char **argv)
 {
  long ll1, ll2;
  WORD err;
  BYTE *cp, *Env;
  union REGS regs;
  if (argc < 2)
  abort();
  /*um einen Buffer ohne 64K-Grenzueberschreitung zu finden, wird dmabuf doppelt
  so groá wie notwendig angelegt und nur die Haelfte ohne Grenze benutzt */
  fpBuff = dmabuf;
  ll1 = (( long)FP_SEG(fpBuff) << 4L) + (long)FP_OFF(fpBuff);
  fpBuff += DMABUFSIZE - 1;
  ll2 = (( long)FP_SEG(fpBuff) << 4L) + (long)FP_OFF(fpBuff);
  fpBuff = dmabuf;
  pBuffer0 = dmabuf;
  pBuffer1 = dmabuf + (DMABUFSIZE / 2);
  pBuff2 = dmabuf + DMABUFSIZE;
  if((ll1 & 0xf0000) != (ll2 & 0xf0000))
   {
    pBuff2 = dmabuf;
    fpBuff = dmabuf + DMABUFSIZE; /* Grenze in 1.Haelfte: 2.Haelfte benutzen */
    pBuffer0 += DMABUFSIZE;
    pBuffer1 += DMABUFSIZE;
   }
  ll1 = (( long)FP_SEG(fpBuff) << 4L) + (long)FP_OFF(fpBuff);
  fpBuff = (void far *)(((ll1 & 0xf0000) << 12) + (ll1 & 0xffff));
  /* Environmentvar. BLASTER auswerten*/
  Env = getenv("BLASTER");
  if (!cp)
   {
    printf("BLASTER nicht gesetzt!");
    exit(0);
   }
  cp = strchr(Env,'A');
  if (cp) sscanf(cp+1,"%x",&DspPort);
  MixPort = DspPort;
  cp = strchr(Env,'M');
  if (cp) sscanf(cp+1,"%x",&MixPort);

  cp = strchr(Env,'P');
  if (cp) sscanf(cp+1,"%x",&MpuPort);
  cp = strchr(Env,'I');
  if(cp) DspIrq  = atoi(cp+1);
  cp = strchr(Env,'D');
  if(cp) DspDmaB  = atoi(cp+1);
  cp = strchr(Env,'H');
  if(cp) DspDmaW = atoi(cp+1);
  if (dsp_Reset())
   {
    printf("Error bei DSP-Reset/n");
    exit(99);
   }
  err = _dos_open(argv[1], O_RDONLY, &handle);
  if (err)
   {
    perror(argv[1]);
    exit(0);
   }
  OldInt = irq_SetHandler(DspIrq, dsp_IrqHandler);

  /*DMA-Doublebuffering initialisieren*/
  BufCnt = 0;
  /* Mixer volle Lautstaerke VOICE */
  mix_Write(0x3c,0);
  mix_Write(0x32,0xe0);
  mix_Write(0x33,0xe0);

  irqs = 0;
  printf("/nGesamt (Min:Sec-Frames): ");
  timeprint(filelength(handle));
  printf("/n"); start_dma_dsp();
  while(1)
   {
    /* warte auf naechsten INT vom DSP*/
    while(!irqs);
    irqs--;
    pakt = (BufCnt++ & 1) ? pBuffer1 : pBuffer0;
    memset(pakt,0,DMABUFSIZE/2);
    err = read(handle,pakt,DMABUFSIZE/2);
    if (err != (DMABUFSIZE/2)) /* EOF */
     {
      stop_playing(); break;
     }
      regs.h.ah = 0xb; /* keyboard stat */
      int86(0x21,&regs,&regs);
      if (regs.h.al) /* Taste gedrueckt */
       {
        regs.h.ah = 7; /* get key */
        int86(0x21,&regs,&regs);
        switch(regs.h.al)
         {
          case 'e': lseek(handle,-1764000L,SEEK_END); /* letzte 10 Sekunden */
                    break;
          case '+': lseek(handle,+1764000L,SEEK_CUR);/* 10 Sek vor */
                    break;
          case '-': lseek(handle,-1764000L,SEEK_CUR);
                    break; /* 10 Sek zur. */
          case 0x1b:stop_playing();/* ESC */
                    irq_SetHandler(DspIrq, OldInt);
                    exit(0);
         }
       }
      timeprint(tell(handle));
     }
    irq_SetHandler(DspIrq, OldInt);
    return(0);
   }


void start_dma_dsp(void)
 {
  /* int DMA-Splitbuffering */
  BufCnt = 0; irqs = 0;
  lseek(handle, 44L, SEEK_SET); /* Skip
  Header der WAVE-Datei */
  read(handle, pBuffer0, DMABUFSIZE); /*
  Beide Halbbuffer fuellen */
  dma_SetChannel(DspDmaW, fpBuff,(DMABUFSIZE)-1, 0x58);
  /* Modus: single + autoinit + read */
  /* Ausgabefrequ.(DAC-Modus) setzen */
  dsp_Write(0x41);
  dsp_Write(HIBYTE(44100));/* Samplefrequenz von CDs */
  dsp_Write(LOBYTE(44100));
  /* DAC+AUTOINIT+USEFIFO+16bit-DMA */
  cmd  = 4 " 2 " 0xb0;
  /* stereo + signed */
  dspmode  = 0x20 " 0x10;
  /* Kommando, Modus und Laenge an DSP*/
  dsp_Write(cmd);
  dsp_Write(dspmode);
  /* Halber Buffer in WORDS bzw. BYTES*/
  dsp_Write(LOBYTE((DMABUFSIZE/4) - 1));
  dsp_Write(HIBYTE((DMABUFSIZE/4) - 1));
 }

void stop_playing(void)
 {
  irqs = 0;
  while(!irqs);
  pakt = (BufCnt++ & 1) ? pBuffer1 : pBuffer0;
  memset(pakt,0,DMABUFSIZE/2); /* Ruhe*/
  irqs = 0;
  while(!irqs);
  dsp_Reset();
  return;
 }

void dsp_Write(WORD iVal)
 {
  while(inp(DspPort + DSP_WRITESTATUS)& 0x80);
  outp(DspPort + DSP_WRITECMDDATA, (BYTE)iVal);
 }

void dsp_Read(BYTE * Val)
 {
  while(!(inp(DspPort + DSP_READSTATUS) & 0x80));
  *Val=(BYTE)inp(DspPort+DSP_READDATA);
 }

WORD dsp_Reset(void)
 {
  BYTE Val = 0; /* Reset-Port zuerst mit 1, dann mit 0 beschreiben */
  outp(DspPort + DSP_RESET, 1);
  inp(DspPort + DSP_RESET);/* etwas  */
  inp(DspPort + DSP_RESET);/* warten */
  inp(DspPort + DSP_RESET);
  outp(DspPort + DSP_RESET, 0);
  inp(DspPort + DSP_RESET);
  inp(DspPort + DSP_RESET);
  inp(DspPort + DSP_RESET);
  dsp_Read(&Val); /* Wert lesen */
  return(Val == 0xAA) ? 0:-1;/*0xAA=OK*/
 }

int dma_adress   [8] = "0x00,0x02,0x04,0x06,0xC0,0xC4,0xC8,0xCC";
int dma_count    [8] = "0x01,0x03,0x05,0x07,0xC2,0xC6,0xCA,0xCE";
int dma_page     [8] = "0x87,0x83,0x81,0x82,0x88,0x8B,0x89,0x8A";
int dma_chmask   [2] = "0x0A,0xD4";
int dma_mode     [2] = "0x0B,0xD6";
int dma_flipflop [2] = "0x0C,0xD8";

void dma_SetMask(int Chan)
 {
  Chan &= 0x0007;
  outp(dma_chmask[Chan / 4], 4 | (Chan & 0x03) );
 }

void dma_ClrMask(int Chan)
 {
  Chan &= 0x0007;
  outp(dma_chmask[Chan/4],Chan & 0x03);
 }

void dma_ClrFlipFlop(int Chan)
 {
  Chan &= 0x0007;
  outp(dma_flipflop[Chan / 4], 0);
 }

void dma_SetChannel(int Chan, void far *mem, WORD Size, BYTE Mode)
 {
  WORD  adr;
  BYTE  page;
  Chan &= 0x0007;/*Max. 8 DMA-Kanaele*/
  dma_SetMask(Chan); /* Kanal sperren*/

   /*DMA uebertr.1 Byte mehr als Size! lineare 20-Bit Adresse erzeugen */
  if(Chan <= 3) /* 8Bit DMA */
   {
    /* Adresse = untere 16 Bit der 20-Bit Adresse */
    adr = (WORD) (((((long) mem) & 0xFFFF0000L) >> 12L) + (((long) mem) & 0xFFFFL));
    /* Seite=obere 4 Bit der 20-Bit Adr.*/
    page = (BYTE) ((((((long) mem) & 0xFFFF0000L) >> 12L) + (((long) mem) & 0xFFFFL)) >> 16);
   }
  else  /* 16-Bit DMA */
   { adr = (WORD) (((((long) mem) & 0xFFFF0000L) >> 13L) + ((((long) mem) & 0xFFFFL) >> 1L));
     page = (BYTE) ((((((long) mem) & 0xFFFF0000L) >> 12L) + (((long) mem) & 0xFFFFL)) >> 16);
     page &= (BYTE) 0xFE;
     Size /= 2; /* Es werden WORDs (nicht BYTEs) gezaehlt! */
    }
   outp(dma_mode[Chan / 4], Mode " (Chan & 3) );
   dma_ClrFlipFlop(Chan);
   outp(dma_adress[Chan], LOBYTE(adr) );
   outp(dma_adress[Chan], HIBYTE(adr) );
   outp(dma_page[Chan], page); /* Page-Latch laden */
   dma_ClrFlipFlop(Chan);
   outp(dma_count[Chan], LOBYTE(Size) );
   outp(dma_count[Chan], HIBYTE(Size) );
   dma_ClrMask(Chan); /* DMA-Kanal wieder freigeben */
 }

void interrupt far dsp_IrqHandler()
 {
  BYTE ursache;
  irqs++;/* Anzahl unverarbeitete Aufrufe erhoehen */
  /* DSP-Version ab 4.00: */
  /* Welcher Chip hat IRQ ausgeloest ?*/
  outp(MixPort + MIX_REGISTERPORT, (BYTE)MIX4_IRQSOURCE);
  ursache = (BYTE)inp(MixPort + MIX_DATAPORT);
  /* 8-Bit DMA oder Midi-Uebertragung */
  if (ursache & MIX4_IRQ8DMA) inp(DspPort + 0x0e);
  /* 16-Bit DMA */
  if (ursache & MIX4_IRQ16DMA) inp(DspPort + 0x0f);
  /* MPU-401 UART */
  if (ursache & MIX4_IRQMPU) inp(MpuPort);
 /* alle DSP-Versionen vor V4.xx nur 1 Verursacher moeglich */
 /*   inp(DspPort + 0x0e); Interruptkontroller das Ende des IR
      Q's signalisieren */
  if (DspIrq > 7) outp(SLAVE_8259, EOI); /*Slave-Ctrl*/
  outp(MASTER_8259, EOI); /* MASTER immer EOI signalisieren */
 }

void irq_onoff(int irq,int on)
 {
  int Port; /* Port des zustaendigen 8259 ermitteln */
   /* (0-7 = MASTER_8259 , 8-15 = SLAVE_ 8259) */
  Port = (irq <= 7) ? MASTER_8259+1 : S
  LAVE_8259+1 ;
  irq &= 0x0007;/* untere 3 bit waehlen  Kanal des entsprechenen 8259 */
  if (on) outp(Port, inp(Port) & á(1 << irq)); /* Bit Loeschen: Interrupt erlaubt */
  else outp(Port, inp(Port) " (1 << irq) ); /* Bit Setzen: Interrupt gesperrt */
 }

void mix_Write(WORD iReg, WORD iData)
 {
  outp(MixPort + MIX_REGISTERPORT,(BYTE) iReg);
  outp(MixPort + MIX_DATAPORT,(BYTE) iData);
 }

void timeprint(long fpos)
 {
  long rest, rest2, rest3;
  fpos -= 44L; /* korrigiere Header */
  rest = fpos / 10584000L;
  fpos -= rest * 10584000L;
  rest2 = fpos / 176400L;
  fpos -= rest2 * 176400L;
  rest3 = fpos / 2352L;
  printf("%2.2ld:%2.2ld-%2.2ld/r",rest,rest2,rest3);
 }
