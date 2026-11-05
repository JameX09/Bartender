# Bartender 3D: Skyline Lounge

3D barmanská hra v Unity 6 (URP). Stojíš za barom na streche mrakodrapu v noci, okolo svieti mesto
a miešaš koktaily ako skutočný barman: šejker, džiger, sitko, miešací pohár, barová lyžica, drvič,
lopatka na ľad, ozdoby a 29 fliaš (destiláty, likéry, vína, sirupy, džúsy, miešačky).

Všetko je vygenerované v kóde: modely (sústružené profily, zaoblené hrany, žiadne kocky), textúry
(mramor, orech, teak, terrazzo, kartáčovaná oceľ, mosadz, meď, koža, látka, presvetlený onyx),
nočná obloha, mesto, zvuky aj hudba. Nie sú tam žiadne cudzie assety, takže hru môžeš publikovať
bez licenčných problémov.

## Spustenie

- **Hra:** `bin\Bartender3D.exe` (alebo **Spustiť** v Project Manageri)
- **Build:** `build.bat` (alebo **Zostaviť** v Project Manageri). Ak máš projekt otvorený v Unity editore, najprv ho zavri.
- **Editor:** otvor priečinok v Unity Hub (verzia 6000.6.5f1). V menu *Bartender 3D* je príprava projektu, prepečenie textúr aj build.
- **Vydanie:** `release.bat` zabalí `bin` do `release\Bartender3D-<verzia>-win64.zip` (napr. na itch.io).

## Režimy

- **Kariéra:** každá noc trvá od 20:00 do 02:00. Hostia si sadnú k baru a objednajú si drink.
  Za dobrý drink dostaneš zaplatené aj sprepitné, keď hosť čaká príliš dlho, odíde.
  Každú noc pribudnú nové recepty (spolu 20) a za zarobené peniaze si kúpiš vylepšenia.
- **Voľné miešanie:** bez hostí a bez času. Polož drink na zlatú podložku na bare a hra ti povie,
  na ktorý recept sa najviac podobá a ako sa ti podaril. V knihe receptov si môžeš vybrať drink na tréning.

## Ovládanie

| Kláves | Akcia |
|---|---|
| Myš | rozhliadanie |
| WASD | chôdza za barom |
| ĽTM | vziať / položiť / použiť (lopatka na ľad, ozdoba, viečko, sitko) |
| Drž PTM | nalievať (zamier na pohár), trepať šejker s viečkom, miešať lyžicou, drviť mätu |
| Shift + PTM | nalievať pomaly; koliesko jemne mení náklon |
| E | pri šejkri v ruke: viečko → sitko → otvorené |
| R | otočiť džiger (60/30, 50/25, 40/20, 30/15, 20/10 ml) |
| H | skryť / zobraziť nápovedy a kartu receptu (aj šípka vpravo hore) |
| Q | vrátiť vec na miesto |
| X | vyliať obsah |
| Tab | kniha receptov |
| F | ďalšia objednávka |
| Esc | pauza |

## Odmeriavanie a espresso

- Džiger vždy ide do **ľavej ruky**. Pravou rukou vezmi fľašu a drž PTM: ak nemieriš na iný pohár, fľaša naleje do džigra a pri okraji sa sama zastaví (dá sa vypnúť v nastaveniach: *Pour assist*).
- Klikni na šejker alebo pohár a džiger sa do neho vyleje. R džiger otočí na menšiu stranu. Na podložke je 5 džigrov: 60/30 (meď), 50/25 (zlato), 40/20 (chróm), 30/15 (čierny), 20/10 (mosadz).
- Rýchlosť nalievania nastavíš v *Settings → Pour speed*; so Shiftom fľaša leje asi tretinovou rýchlosťou. Fľaša nalieva z pravého boku.
- Hostia čakajú približne 2,5 až 4,5 minúty (prvé noci ešte dlhšie), noc trvá 8 minút.
- Lopatkou naberieš ľad (animácia) a kliknutím na pohár alebo šejker kocky nasypeš.
- Kávovar na zadnom bare (vľavo) po kliknutí pripraví 30 ml espressa do šálky, ktorú potom vyleješ do šejkra (Espresso Martini).

## Ako to funguje

- **Tekutiny** majú skutočný objem v ml. Hladina sa počíta prerezaním vnútra pohára rovinou, takže pri
  naklonení ostáva vodorovná a tekutina vyteká cez najnižší bod okraja. Farba sa mieša podľa
  ingrediencií, ľad zvyšuje hladinu, perlivé drinky bublinkujú.
- **Hodnotenie** (0–100, 1–5 hviezdičiek): presnosť ml (50 b.), správny pohár (15), postup: trasenie,
  miešanie, drvenie a precedenie (15), ľad (10), ozdoba (10).
- **Karta receptu** vpravo počas hrania odškrtáva kroky podľa pohára alebo šejkra, ktorý práve používaš.

## Štruktúra

```
Assets/Scripts/Core     ingrediencie, recepty a hodnotenie, obsah pohára, ukladanie, preklady
Assets/Scripts/Gfx      generovanie textúr, meshov a materiálov, riešič hladiny, efekty, text do textúr
Assets/Scripts/Items    predmety (fľaše, poháre, šejker, sitko ...) a stanice (ľad, ozdoby, drez ...)
Assets/Scripts/World    bar, lounge, mesto, svetlá, post-processing
Assets/Scripts/Guests   postavy a logika kariérnej noci
Assets/Scripts/Player   pohyb, držanie, nalievanie, trasenie, miešanie
Assets/Scripts/UI       menu, HUD, kniha receptov, nastavenia, obchod
Assets/Scripts/Audio    syntetizované zvuky, ambient a lounge hudba
Assets/Editor           BuildScript: prepečie textúry a materiály, pripraví scénu a zostaví .exe
```

Uloženie (kariéra a nastavenia) je v registri cez PlayerPrefs: `HKCU\Software\TheProjects\Bartender 3D`.

Testovacie screenshoty: `bin\Bartender3D.exe -shots <priečinok>` urobí sériu záberov a hru zavrie.
