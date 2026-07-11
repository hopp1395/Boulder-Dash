#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <dos.h>
#include <mem.h>
#include <ctype.h>

unsigned char *zeiger=NULL;
FILE *quelle;
FILE *ziel;


void hilfe (void)
 {
  printf("\nFalsche Parameter\n");
  printf("SYNTAX: CONVATB Quelldatei Zieldatei\n\n");
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

void main (char argc, char *argv[2])
 {
   long i;
   long groesse;

   printf("\n\nBoulder Dash Level Konverter v1.0");
   printf("\nProgramm by Jan Hoppe 1998. All rights reserved.\n");

   if (argc!=3) hilfe();

   if ((quelle=fopen(argv[0],"r+b"))==NULL)
      printf("\nDatei > %s < existiert nicht!\n\n",argv[0]), ende(0);

   if ((ziel=fopen(argv[1],"wb"))==NULL)
      printf("\nDatei > %s < kann nicht angelegt werden!\n",argv[1]), ende(0);

   fseek(quelle,(long)0,2);
   groesse=ftell(quelle);
   fseek(quelle, SEEK_SET, 0);

   printf("\nDateigr”áe   : %lu Bytes",groesse);
   printf("\nLevel Anzahl : %i Levels\n",groesse/(40*22+2)-16);

   zeiger = (unsigned char *) calloc(groesse, sizeof (unsigned char));

   if (zeiger==NULL)
     printf("\nNicht gengend Speicher frei!"), ende(0);

   if ((fread (zeiger,1,groesse,quelle)) == NULL )
     printf("\nFehler beim lesen der Datei!"), ende(0);


   for (i=0;i<groesse;i++) *(zeiger+i)=*(zeiger+i)-0x30;

   if ((fwrite (zeiger,1,groesse,ziel)) == NULL)
      printf("\nFehler beim schreiben der Datei > %s <",argv[1]), ende(0);

   ende(1);
  }
