10 LET A(sin 4)) = 28*C(
19 LET a=INT(RND*28)+1 
11 PRINT INT (B + SIN 4) * 5
11 PRINT #3," pulsa una tecla para continuar"
12 LET OP=1 : OPEN #3,"Fichero"
13 CLOSE #3
14 PRINT SIN 13
15 GO TO 20 : GO TO20 : GOTO 20: GOTO20
16 GO SUB 20 : GO SUB20 : GOSUB 20: GOSUB20

1 REM *******************
2 REM * Cesar Hernandez *
3 REM *  {7F} MONSER S.A.  *
4 REM *   Tragaperras   *
5 REM *******************
6 REM
7 REM
22 REM "**rutina instrucciones***"
23 REM
25 BORDER 6: PAPER 2: INK 6: CLS
40 PRINT AT 5,5;"I N S T R U C C I O N E S"
45 PRINT AT 7,0;"Tienes 500 pts. inicialmente."
50 PRINT AT 8,0;"Puedes jugar cualquier cantidad."
55 PRINT AT 9,0;"Si coinciden dos 1as. capitales"
60 PRINT AT 11,3;"Multiplicas tu apuesta por 3"
65 PRINT AT 13,0;"Si coinciden tres capitales"
70 PRINT AT 15,3;"Multiplicas apuesta por 10"
75 PRINT AT 17,0;"En otro caso,pierdes lo apostado"
80 PRINT AT 19,10;" ahora... S U E R T E"
290 INK 5
300 PRINT #1;" pulsa una tecla para continuar"
310 IF INKEY$<>"" THEN GO TO 500
320 GO TO 310
500 LET dinero=500: CLS
1000 REM
1001 REM ***  apuesta  ***
1002 REM
1005 INK 5: PAPER 0: BEEP .2,0: CLS
1010 CLS: PRINT AT 15,3;"Dispones de: ";dinero;" pesetas.": BEEP .3,0
1015 INPUT "Cuanto apuestas esta vez? ";apuesta
1020 IF apuesta>dinero THEN GO TO 1010
1030 IF apuesta<1 THEN GO TO 1010
1050 PRINT AT 17,3;"Veamos que suerte tienes               ": BEEP .2,5
1055 PAUSE 100
1057 REM
1058 REM * voy a buscar nombre *
1060 GO SUB 6000
1065 PLOT 10,120: DRAW 0,20: DRAW 60,0: DRAW 0,-20: DRAW -60,0
1070 PLOT 90,120: DRAW 0,20: DRAW 60,0: DRAW 0,-20: DRAW -60,0
1075 PLOT 170,120: DRAW 0,20: DRAW 60,0: DRAW 0,-20: DRAW -60,0
1080 PRINT AT 5,2;c$
1085 PRINT AT 5,12;p$
1090 PRINT AT 5,22;k$
1092 PAUSE 50
1100 IF c$<>p$ THEN GO TO 2000
1110 IF p$=k$ THEN GO TO 1500
1120 PRINT AT 17,1;"Enhorabuena, solo fallaste una!         ": BEEP .2,0
1125 PAUSE 100
1130 LET apuesta=apuesta*3
1140 LET dinero=dinero+apuesta
1150 GO TO 1010
1500 PRINT AT 17,1;"Maravilloso, diste en la diana!          ": BEEP .2,5
1505 PAUSE 100
1510 LET apuesta=apuesta*10: GO TO 1140
2000 PRINT AT 17,1;"Lo siento,no hubo suerte,repite": BEEP .3,0
2005 PAUSE 150
2010 LET dinero=dinero-apuesta
2020 IF dinero=0 THEN GO TO 2100
2030 GO TO 1010
2110 PRINT AT 18,0;"Ademas, dejas de jugar"
2120 PRINT AT 19,0;"Te quedaste sin nada"
2121 FOR m=1 TO 20
2122 BEEP .2,2
2123 NEXT m
2130 GO TO 300
6000 REM
6001 REM *** Buscar nombres ***
6002 REM
6010 RESTORE 7000
6020 LET a=INT(RND*28)+1
6030 FOR n=1 TO a
6040 READ c$: NEXT n
6050 RESTORE 8000
6060 LET a=INT(RND*28)+1
6070 FOR n=1 TO a
6080 READ p$: NEXT n
6090 RESTORE 9000
6100 LET a=INT(RND*28)+1
6110 FOR n=1 TO a
6120 READ k$: NEXT n
6180 RETURN
6500 REM
6501 REM aqui van los datas
6502 REM
7000 DATA "madrid","madrid","cuenca","cuenca","toledo","madrid","cuenca","toledo","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca"
8000 DATA "madrid","madrid","cuenca","cuenca","toledo","madrid","cuenca","toledo","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca"
9000 DATA "madrid","madrid","cuenca","cuenca","toledo","madrid","cuenca","toledo","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca","madrid","toledo","cuenca"