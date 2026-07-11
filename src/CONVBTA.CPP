/**************************************************************************/
/* Programmm    : CONVBTA.EXE                                             */
/* Autor        : Jan Hoppe                                               */
/* Version      : 1.0                                                     */
/* Beschreibung : Konvertier Bin„r Levels in Asciformat                   */
/**************************************************************************/

#include <stdio.h>
#include <stdlib.h>
#include <mem.h>

unsigned char *zeiger=NULL;
FILE *quelle;
FILE *ziel;

void hilfe (void)
 {
  printf("\nFalsche Parameter\n");
  printf("SYNTAX: CONVBTA Quelldatei Zieldatei\n\n");
  exit(0);
 }

void ende (int error)
 {
  fclose(quelle);
  fclose(ziel);
  free(zeiger);
  if (error==0) printf("\nEs sind Fehler aufgetreten!");
  else  printf("\nConvert abgeschloáen");
  printf("\n\n");
  exit(0);
 }

void main (char argc, char *argv[3])
 {
   long i;
   long groesse;

   char *qd=argv[1];
   char *zd=argv[2];

   printf("\n\nBoulder Dash Level Konverter v1.0");
   printf("\nProgramm by Jan Hoppe 1998. All rights reserved.\n");

   if (argc!=3) hilfe();

   if ((quelle=fopen(qd,"r+b"))==NULL)
      printf("\nDatei > %s < existiert nicht!\n\n",qd), ende(0);

   if ((ziel=fopen(zd,"wb"))==NULL)
      printf("\nDatei > %s < kann nicht angelegt werden!\n",zd), ende(0);

   fseek(quelle,(long)0,2);
   groesse=ftell(quelle);
   fseek(quelle, SEEK_SET, 0);

   printf("\nQuelldatei   : %s",qd);
   printf("\nZieldatei    : %s",zd);
   printf("\nDateigr”áe   : %lu Bytes",groesse);
   printf("\nLevel Anzahl : %i Levels\n",groesse/(40*22+2)-16);

   zeiger = (unsigned char *) calloc(groesse, sizeof (unsigned char));

   if (zeiger==NULL)
     printf("\nNicht gengend Speicher frei!"), ende(0);

   if ((fread (zeiger,1,groesse,quelle)) == NULL )
     printf("\nFehler beim lesen der Datei!"), ende(0);


   for (i=0;i<groesse;i++) *(zeiger+i)=*(zeiger+i)+0x30;

   if ((fwrite (zeiger,1,groesse,ziel)) == NULL)
      printf("\nFehler beim schreiben der Datei > %s <",zd), ende(0);

   ende(1);
  }
