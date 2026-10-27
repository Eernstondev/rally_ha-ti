# Rally Ayiti — Prototype

Tout est automatique : à la première ouverture, Unity installe les outils nécessaires et **construit le circuit tout seul** (route, décor, 6 karts, caméra, boutons tactiles). Tu n'as rien à monter à la main.

## Ce que tu dois faire (environ 30 à 45 minutes, surtout du téléchargement)

### 1. Installer Unity Hub
- Va sur **unity.com/download** et télécharge **Unity Hub** pour Mac.
- Installe-le et ouvre-le.
- Crée un compte Unity (gratuit) et connecte-toi. Choisis la licence **Personal** (gratuite) si on te le demande.

### 2. Installer Unity 6
- Dans Unity Hub : **Installs → Install Editor**.
- Choisis la dernière version **Unity 6 LTS** (6000.x, marquée « LTS »).
- Dans les modules, coche **Android Build Support** (avec OpenJDK et Android SDK & NDK). Ça servira pour mettre le jeu sur ton téléphone.
- Lance l'installation et attends la fin (plusieurs Go).

### 3. Ouvrir le projet
- Décompresse `rally-ayiti-prototype.zip` (par exemple dans ton dossier Documents).
- Dans Unity Hub : **Projects → Add → Add project from disk**, choisis le dossier **`rally-ayiti-unity`**.
- Clique sur le projet pour l'ouvrir. Si Unity Hub demande une version, choisis la **Unity 6** que tu viens d'installer.
- La première ouverture prend quelques minutes.

### 4. Les fenêtres à valider
- Si Unity demande **« enable the new Input System backends? »** ou **« Restart? »**, clique **Yes**. Unity redémarre.
- Unity passe le projet en rendu URP et construit les deux circuits tout seul (quelques minutes la première fois).

### 5. Jouer
- Ouvre la scène **Assets/_Project/Scenes/Meni** et appuie sur **▶ Play** (en haut au milieu).
- Au menu : **← →** pour changer de pilote, **↑ ↓** (ou clic sur une carte) pour le circuit, **Entrée** ou **ALE !** pour partir.
- En course :
  - **Q / D** ou **flèches** : tourner
  - **Espace** maintenu dans un virage : drifter (relâche pour le boost)
  - **E** ou **Maj** : lancer le bonus
  - **H** : klaxon
  - **S** : freiner
- À l'arrivée : tableau des résultats, **REJWE** (rejouer) ou **MENI**.
- Appuie encore sur **▶** pour arrêter.

Tu peux aussi lancer directement **Labadie** ou **Prototype** : tu joues alors le dernier pilote choisi (Ti Djo par défaut).

Pour tout reconstruire à neuf : menu **Rally Ayiti → Tout construire (menu + circuits)**.

## Menu de départ

Logo, kart du pilote sur un plateau tournant au milieu d'une petite place (maisons, cocotiers, bougainvilliers, curieux), fiche du pilote (véhicule, devise, barres Vitès / Akselerasyon / Kontwòl) et cartes des circuits. Musique twoubadou. Le choix est gardé d'une partie à l'autre.

## Sons

Tous créés pour le jeu (aucun échantillon externe) :
- Moteur qui monte en régime et passe les rapports, plus grave pour le tap-tap, plus aigu pour la moto
- Crissement de pneus en drift, souffle du boost, klaxon (les pilotes IA klaxonnent parfois quand tu es à côté, parfois la mélodie de tap-tap)
- Bips du décompte, musique konpa dès le départ, fanfare à l'arrivée
- Ambiance mer à Labadie, ambiance de rue dans le quartier
- Bonus : ramassage, lancer de Kokoye, impact, mangue écrasée

## Bonus

Traverse une caisse **?** pour recevoir un bonus (affiché en haut au centre) :
- **Kokoye** : noix de coco lancée tout droit, elle rebondit sur les murs. Le kart touché fait un tête-à-queue.
- **Mango tonbe** : trois mangues laissées derrière toi. Le premier qui roule dessus dérape.

Plus tu es loin dans le classement, plus tu as de chances de recevoir un Kokoye. Les pilotes IA utilisent aussi leurs bonus.

## Labadie

Plage de sable entre la piste et la mer, mer animée (houle, reflets qui défilent, dégradé turquoise vers bleu profond), écume qui monte et se retire sur le sable, canots « Bondye Beni » sur la plage et au mouillage, arche « Byenveni Labadi » sur la ligne de départ, saut au bord de l'eau, mornes verts autour de la baie.

## Si quelque chose ne marche pas
- **Erreurs rouges dans la fenêtre Console** : fais une capture d'écran et envoie-la-moi.
- **Les karts ne bougent pas** : vérifie que la fenêtre Input System a bien été validée (étape 4), puis relance Unity.
- **Tout est rose** : envoie-moi une capture, c'est un réglage de rendu à corriger.

## Mettre à jour un projet déjà ouvert

1. Ferme Unity.
2. Remplace le dossier `Assets/_Project` de ton projet par celui du nouveau zip.
3. Rouvre le projet : le circuit se reconstruit tout seul avec les nouveaux graphismes (une seule fois).

Tu peux aussi tout reconstruire quand tu veux : menu **Rally Ayiti → Tout construire (menu + circuits)**.

## Graphismes

- **Véhicules** : tap-tap peint (« Bondye Bon », « Mèsi Jezi », « Jezi se chèf »), moto taxi, buggy de plage. Les roues tournent et braquent.
- **Pilotes** : casque intégral, couleurs propres à chaque pilote.
- **Décor** : maisons en béton avec réservoir noir et fers d'attente, maison à étage avec balcon, Boutik Lakay, cahute en bois et tôle, murs en parpaings, étals de marché, parasols, cocotiers, poteaux et fils électriques, piles de pneus.
- **Circuit** : asphalte texturé, bordures rouges et blanches, murets en parpaings, sable.
- **Entourage** : sol peint (terre au bord de la piste, herbe plus ou moins sèche, sable vers la mer) avec grain de détail, manguiers en bosquets, bananiers, bougainvilliers en fleurs, buissons, touffes d'herbe, rochers, clôtures en bois, hameaux, tap-taps et motos garés.
- **Départ** : gradins pleins de supporters, foule derrière le muret, guirlandes de fanions, panneaux publicitaires (Kafe Lakay…).
- **Lumière** : rendu URP, ciel dégradé avec soleil, espace de couleur linéaire, ombres douces, lumière ambiante chaude.
- **Post-traitement** : bloom, tonemapping ACES, contraste et saturation renforcés, balance des blancs chaude, vignette. Réglable dans `Assets/_Project/Settings/RallyAyiti_PostFX`.

Les modèles sont dans `Assets/_Project/Art/Models` (FBX) et les textures dans `Assets/_Project/Art/Textures`. Tu peux les ouvrir dans Blender pour les retoucher.

## Contenu
```
Assets/_Project/
  Art/          Models (31 FBX), Textures (57 PNG), Audio (17 WAV), Fonts (Bungee, licence OFL), Generated (sols peints)
  Editor/       PrototypeBuilder (+ .Menu, .Scenery), RenderSetup (URP + post-traitement), ArtLibrary, ArtImport
  Scripts/
    Audio/        GameAudio, KartAudio, RaceAudio
    Core/         GameSettings, RaceSetup
    Environment/  WaterSurface, ShoreFoam, Bobbing
    Input/        IKartInput, PlayerKartInput, AIDriver
    Kart/         KartController, KartStats, KartBodyLink, KartEffects, KartCameraFx, KartWheels
    PowerUps/     PowerUpSystem, ItemBox, KokoyeProjectile, KokoyeHitbox, MangoTrap, PowerUpType
    Race/         RaceManager, LapTracker, Checkpoint, BoostPad
    UI/           MainMenu, RaceHUD, ItemHUD
Packages/manifest.json   (URP, Input System, Cinemachine, Splines)
```
